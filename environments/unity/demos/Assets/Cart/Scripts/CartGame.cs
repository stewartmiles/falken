// Copyright 2021 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// <c>CartGame</c> Creates cars and manages the state of the race.
/// </summary>
public class CartGame : MonoBehaviour
{
    [Tooltip("The track upon which to race.")]
    public SplineTrack track;
    [Tooltip("The car prefab to instantiate at the start of the game.")]
    public CarController carPrefab;
    [Tooltip("The chase camera prefab to instantiate at the start of the game.")]
    public CarCamera cameraPrefab;

    private CarController car;
    private CarCamera chaseCamera;
    private int nextCheckpointIndex;

    void Start()
    {
        if (track && carPrefab && cameraPrefab) {
            if (Camera.main) {
                Camera.main.gameObject.SetActive(false);
            }
            Transform[] controlPoints = track.GetControlPoints();
            Transform startingPoint = controlPoints[0];
            car = GameObject.Instantiate(carPrefab, startingPoint.position, startingPoint.rotation);
            chaseCamera = GameObject.Instantiate(cameraPrefab, startingPoint.position,
                startingPoint.rotation);
            chaseCamera.target = car.GetComponent<Rigidbody>();
            nextCheckpointIndex = 1;
        } else {
            Debug.Log("CartGame is not configured properly. Please set a track, car, and camera.");
        }
    }

    void Update() {
        if (track && car) {
            Transform[] controlPoints = track.GetControlPoints();
            Transform checkpoint = controlPoints[nextCheckpointIndex];
            Vector3 carToCheckpoint = (car.transform.position - checkpoint.position).normalized;
            if(Vector3.Dot(checkpoint.forward, carToCheckpoint) > 0) {
                ++nextCheckpointIndex;
                if (nextCheckpointIndex >= controlPoints.Length) {
                    nextCheckpointIndex = 0;
                    Debug.Log("Completed a lap!");
                }
            }
        }
    }
}