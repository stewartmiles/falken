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
#
# automatically generated by the FlatBuffers compiler, do not modify

# namespace: tflite

import flatbuffers
from flatbuffers.compat import import_numpy
np = import_numpy()

class Rfft2dOptions(object):
    __slots__ = ['_tab']

    @classmethod
    def GetRootAsRfft2dOptions(cls, buf, offset):
        n = flatbuffers.encode.Get(flatbuffers.packer.uoffset, buf, offset)
        x = Rfft2dOptions()
        x.Init(buf, n + offset)
        return x

    @classmethod
    def Rfft2dOptionsBufferHasIdentifier(cls, buf, offset, size_prefixed=False):
        return flatbuffers.util.BufferHasIdentifier(buf, offset, b"\x54\x46\x4C\x33", size_prefixed=size_prefixed)

    # Rfft2dOptions
    def Init(self, buf, pos):
        self._tab = flatbuffers.table.Table(buf, pos)

def Rfft2dOptionsStart(builder): builder.StartObject(0)
def Rfft2dOptionsEnd(builder): return builder.EndObject()


class Rfft2dOptionsT(object):

    # Rfft2dOptionsT
    def __init__(self):
        pass

    @classmethod
    def InitFromBuf(cls, buf, pos):
        rfft2dOptions = Rfft2dOptions()
        rfft2dOptions.Init(buf, pos)
        return cls.InitFromObj(rfft2dOptions)

    @classmethod
    def InitFromObj(cls, rfft2dOptions):
        x = Rfft2dOptionsT()
        x._UnPack(rfft2dOptions)
        return x

    # Rfft2dOptionsT
    def _UnPack(self, rfft2dOptions):
        if rfft2dOptions is None:
            return

    # Rfft2dOptionsT
    def Pack(self, builder):
        Rfft2dOptionsStart(builder)
        rfft2dOptions = Rfft2dOptionsEnd(builder)
        return rfft2dOptions