# Copyright 2021 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

# Lint as: python3
"""Launches Falken services."""
import os
import signal
import subprocess
import sys

from absl import app
from absl import flags
from absl import logging
import common.generate_protos  # pylint: disable=unused-import
import common.pip_installer  # pylint: disable=unused-import

FLAGS = flags.FLAGS

flags.DEFINE_integer('port', 50051,
                     'Port for the Falken service to accept RPCs.')
flags.DEFINE_string('ssl_dir',
                    os.path.dirname(os.path.abspath(__file__)),
                    'Path containing the SSL cert and key.')
flags.DEFINE_bool('clean_up_protos', False,
                  'Clean up generated protos at stop.')


def check_ssl():
  """Check if the SSL cert and key exists and request them if they are not.

  Raises:
    FileNotFoundError: when at the end of the operation the key or cert are not
      found.
  """
  key_file = os.path.join(FLAGS.ssl_dir, 'key.pem')
  cert_file = os.path.join(FLAGS.ssl_dir, 'cert.pem')
  if os.path.isfile(key_file) and os.path.isfile(cert_file):
    return

  logging.debug(
      'Cannot find %s and %s, so requesting certificates using openssl.',
      key_file, cert_file)
  try:
    subprocess.run(
        ['openssl', 'req', '-x509', '-newkey', 'rsa:4096', '-keyout', key_file,
         '-out', cert_file, '-days', '365', '-nodes', '-subj',
         '/CN=localhost'], check=True)
  except subprocess.CalledProcessError:
    logging.exception()
    logging.error('Please install openssl at openssl.org.')

  if not os.path.isfile(key_file):
    raise FileNotFoundError('Key was not created.')
  if not os.path.isfile(cert_file):
    raise FileNotFoundError('Cert were not created.')


def run_api(current_path):
  """Start the API in a subprocess.

  Args:
    current_path: The path of the file being executed. Passed as the cwd of the
      subprocess.

  Returns:
    Popen instance where the API service is running.
  """
  return subprocess.Popen(
      [sys.executable, '-m', 'api.falken_service', '--port', str(FLAGS.port),
       '--ssl_dir', FLAGS.ssl_dir, '--verbosity', str(FLAGS.verbosity),
       '--alsologtostderr'], env=os.environ, cwd=current_path)


def run_learner(current_path):
  """Start the learner in a subprocess.

  Args:
    current_path: The path of the file being executed. Passed as the cwd of the
      subprocess.

  Returns:
    Popen instance where the learner service is running.
  """
  return subprocess.Popen(
      [sys.executable, '-m', 'learner.learner_service', '--verbosity',
       str(FLAGS.verbosity), '--alsologtostderr'],
      env=os.environ, cwd=current_path)


def main(argv):
  if len(argv) > 1:
    logging.error('Non-flag parameters are not allowed.')

  logging.debug('Starting Falken services. Press ctrl-c to exit.')
  file_dir = os.path.dirname(os.path.abspath(__file__))
  check_ssl()
  api_process = run_api(file_dir)
  learner_process = run_learner(file_dir)
  while True:
    try:
      _ = sys.stdin.readline()  # Wait for keyboard input.
    except KeyboardInterrupt:
      logging.debug('Cleaning up...')
      if FLAGS.clean_up_protos:
        common.generate_protos.clean_up()
      api_process.send_signal(signal.SIGINT)
      learner_process.send_signal(signal.SIGINT)
      break


if __name__ == '__main__':
  app.run(main)
