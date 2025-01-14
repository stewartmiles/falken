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

// Comment out to use manually computed egocentric actions and observations.
#define EGO_EXP

using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public enum FPGoalState {
    Exit,
    HiddenEnemy,
    BlockedEnemy,
    VisibleEnemy
}

public class FirstPersonEntity : Falken.EntityBase {
    public void UpdateFrom(Transform transform) {
        position = transform.position;
        rotation = transform.rotation;
    }
}

/// <summary>
/// Player-specific entity.
/// </summary>
public class FirstPersonPlayerEntity : FirstPersonEntity {
    public Falken.Feelers feelers = new Falken.Feelers(10.0f, 0.0f, 360.0f, 15, null);
}

/// <summary>
/// Enemy-specific entity.
/// </summary>
public class FirstPersonGoalEntity : FirstPersonEntity {
    public Falken.Category state = new Falken.Category(System.Enum.GetNames(typeof(FPGoalState)));
#if !EGO_EXP
    public Falken.Number distance = new Falken.Number(0f, 100f);
    public Falken.Number angleHoriz = new Falken.Number(-1f, 1f);
    public Falken.Number angleVert = new Falken.Number(-1, 1f);
#endif
}

/// <summary>
/// Observations to allow
/// </summary>
public class FirstPersonObservations : Falken.ObservationsBase {
    public FirstPersonGoalEntity goal;
#if EGO_EXP
    public FirstPersonEntity camera;
#endif

    public FirstPersonObservations() {
        player = new FirstPersonPlayerEntity();
        goal = new FirstPersonGoalEntity();
#if EGO_EXP
        camera = new FirstPersonEntity();
#endif
    }
}

/// <summary>
/// Reflects actions to/from the Falken service.
/// </summary>
public class FirstPersonActions : Falken.ActionsBase {
#if EGO_EXP
    public Falken.Joystick move = new Falken.Joystick(
        Falken.AxesMode.DirectionXZ, Falken.ControlledEntity.Player, Falken.ControlFrame.Player);
    public Falken.Joystick look = new Falken.Joystick(
        Falken.AxesMode.DeltaPitchYaw, Falken.ControlledEntity.Camera);
#else
    public Falken.Number moveX = new Falken.Number(-1f, 1f);
    public Falken.Number moveY = new Falken.Number(-1f, 1f);
    public Falken.Number lookX = new Falken.Number(-1f, 1f);
    public Falken.Number lookY = new Falken.Number(-1f, 1f);
#endif
    public Falken.Boolean fire = new Falken.Boolean();
}

public class FirstPersonBrainSpec : Falken.BrainSpec<FirstPersonObservations, FirstPersonActions> {
}

/// <summary>
/// <c>FirstPersonPlayer</c> Player controller for a first-person style game.
/// </summary>
[RequireComponent(typeof(CharacterController))]
[RequireComponent(typeof(Health))]
public class FirstPersonPlayer : MonoBehaviour
{
    [Tooltip("The speed at which the player moves while running (the default).")]
    [Range(0, 100)]
    public float runningSpeed = 11f;
    [Tooltip("The speed at which the player leaves the ground when jumping.")]
    [Range(0, 100)]
    public float jumpSpeed = 8f;
    [Tooltip("The strength of gravity to apply to the player.")]
    [Range(0, 100)]
    public float gravity = 20f;

    [Tooltip("The camera object to use in lookoing, aiming, and firing.")]
    public Camera playerCamera;
    [Tooltip("The rate at which the camera should rotate based on player input.")]
    [Range(0, 10)]
    public float lookSpeed = 2f;
    [Tooltip("The maximum angle a player can above or below the horizon.")]
    [Range(0, 80)]
    public float lookPitchLimit = 45f;

    [Tooltip("The maximum distance to scan for reticle targets.")]
    public Texture reticleTexture;
    [Tooltip("The size of the reticle (in normailzed device coords).")]
    [Range(0, 0.5f)]
    public float reticleSize = 0.1f;
    [Tooltip("The alpha value of the reticle.")]
    [Range(0, 1)]
    public float reticleAlpha = 0.5f;
    [Tooltip("The maximum distance to scan for reticle targets.")]
    [Range(0, 1000)]
    public float reticleMaxRange = 100f;
    [Tooltip("The weapon class to use when firing.")]
    public Weapon weapon;
    [Tooltip("Whether to render the informational HUD.")]
    bool drawHUD = true;

    private CharacterController characterController;
    private Health health;
    private Vector3 moveDirection = Vector3.zero;
    private float rotationX;
    private GameObject reticleTarget;
    private Vector3 reticleHit;
    private Room currentRoom;
    private Enemy currentEnemy;
    private bool enemyVisible;

    private FirstPersonBrainSpec brainSpec;
    private Falken.Episode episode;
    private bool humanControlled;

    private static List<FirstPersonPlayer> players = new List<FirstPersonPlayer>();

    /// <summary>
    /// Provides a BrainSpec for this player to populate.
    /// </summary>
    public FirstPersonBrainSpec BrainSpec { set { brainSpec = value; } }

    /// <summary>
    /// Provides an Episode for this player to step.
    /// </summary>
    public Falken.Episode Episode { set { episode = value; } }

    /// <summary>
    /// Determins whether a human or Falken should control the player.
    /// </summary>
    public bool HumanControlled { set { humanControlled = value; } }

    /// <summary>
    /// Returns the position at which to aim at this player.
    /// </summary>
    public Vector3 TargetPos { get { return playerCamera.transform.position; } }

    /// <summary>
    /// Returns the Room the player currently occupies.
    /// </summary>
    public Room CurrentRoom {
        get {
            if (!currentRoom) {
                UpdateRoom();
            }
            return currentRoom;
        }
    }

    /// <summary>
    /// Returns a list of all enabled FirstPersonPlayers in the world.
    /// </summary>
    public static List<FirstPersonPlayer> Players { get { return players; } }

    void OnEnable() {
        characterController = GetComponent<CharacterController>();
        health = GetComponent<Health>();
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        players.Add(this);
    }

    void OnDisable() {
        characterController = null;
        health = null;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        players.Remove(this);
    }

    void FixedUpdate() {
        UpdateRoom();
        const float maxEnemyDistance = 20f;
        UpdateEnemy(maxEnemyDistance);

        Vector2 move = Vector2.ClampMagnitude(
            new Vector2(Input.GetAxis("MoveX"), Input.GetAxis("MoveY")), 1f);
        Vector2 look = Vector2.ClampMagnitude(
            new Vector2(Input.GetAxis("LookX"), -Input.GetAxis("LookY")), 1f);
        bool jump = Input.GetButton("Jump");
        bool fire = Input.GetButtonDown("Trigger");

        if (brainSpec != null && episode != null && !episode.Completed) {
            FirstPersonObservations observations = brainSpec.Observations;
            FirstPersonPlayerEntity playerEntity = (FirstPersonPlayerEntity)observations.player;
            playerEntity.UpdateFrom(transform);
            Vector3 feelersOffset = new Vector3(0.0f, 0.5f, 0.0f);
            playerEntity.feelers.Update(transform, feelersOffset, true);

            if (currentRoom && currentRoom.Cleared) {
                if (currentRoom.Exit) {
                    observations.goal.UpdateFrom(currentRoom.Exit.transform);
                } else {
                    observations.goal.position = currentRoom.Bounds.center;
                    observations.goal.rotation = Quaternion.identity;
                }
                observations.goal.state.Value = (int)FPGoalState.Exit;
            } else if (currentEnemy) {
                observations.goal.UpdateFrom(currentEnemy.transform);
                if (enemyVisible) {
                    observations.goal.state.Value = (int)FPGoalState.VisibleEnemy;
                } else if (currentEnemy.Firing) {
                    observations.goal.state.Value = (int)FPGoalState.BlockedEnemy;
                } else {
                    observations.goal.state.Value = (int)FPGoalState.HiddenEnemy;
                }
            } else {
                // Edge case handling based on order of update issues.
                observations.goal.UpdateFrom(playerCamera.transform);
                observations.goal.state.Value = (int)FPGoalState.Exit;
            }

#if EGO_EXP
            observations.camera.UpdateFrom(playerCamera.transform);
#else
            Vector3 playerToGoal = observations.goal.position - playerCamera.transform.position;
            float distanceSqr = playerToGoal.sqrMagnitude;
            if (distanceSqr > Mathf.Epsilon)
            {
                observations.goal.distance.Value = Mathf.Sqrt(distanceSqr);
                playerToGoal /= observations.goal.distance.Value;

                playerToGoal = playerCamera.transform.InverseTransformDirection(playerToGoal);
                observations.goal.angleHoriz.Value = playerToGoal.x;
                observations.goal.angleVert.Value = playerToGoal.y;
            } else {
                // No goal available.
                observations.goal.distance.Value = 0f;
                observations.goal.angleHoriz.Value = 0f;
                observations.goal.angleVert.Value = 0f;
            }
#endif

            FirstPersonActions actions = brainSpec.Actions;
            if (humanControlled) {
#if EGO_EXP
                actions.move.X = move.x;
                actions.move.Y = move.y;
                actions.look.X = look.x;
                actions.look.Y = look.y;
#else
                actions.moveX.Value = move.x;
                actions.moveY.Value = move.y;
                actions.lookX.Value = look.x;
                actions.lookY.Value = look.y;
#endif
                actions.fire.Value = fire;
                actions.ActionsSource = Falken.ActionsBase.Source.HumanDemonstration;
            } else {
#if EGO_EXP
                actions.move.X = 0f;
                actions.move.Y = 0f;
                actions.look.X = 0f;
                actions.look.Y = 0f;
#else
                actions.moveX.Value = 0f;
                actions.moveY.Value = 0f;
                actions.lookX.Value = 0f;
                actions.lookY.Value = 0f;
#endif
                actions.fire.Value = false;
                actions.ActionsSource = Falken.ActionsBase.Source.BrainAction;
            }

            episode.Step();

            if (!humanControlled) {
#if EGO_EXP
                move.x = actions.move.X;
                move.y = actions.move.Y;
                look.x = actions.look.X;
                look.y = actions.look.Y;
#else
                move.x = actions.moveX.Value;
                move.y = actions.moveY.Value;
                look.x = actions.lookX.Value;
                look.y = actions.lookY.Value;
#endif
                jump = false;
                fire = actions.fire.Value;
            }
        }

        Move(move, jump);
        Look(look);
        if (fire) {
            Fire();
        }
    }

    void OnGUI() {
        if (!drawHUD) {
            return;
        }

        if (reticleTexture) {
            Color reticleColor = (reticleTarget && reticleTarget.GetComponent<Health>()) ?
                Color.red : Color.white;
            reticleColor.a = reticleAlpha;
            GUI.color = reticleColor;
            int reticlePixels = (int)(Screen.width * reticleSize);
            Rect reticleRect = new Rect(Screen.width * 0.5f - reticlePixels * 0.5f,
                Screen.height * 0.5f - reticlePixels * 0.5f,
                reticlePixels, reticlePixels);
            GUI.DrawTexture(reticleRect, reticleTexture);
        }

        if (!health.infiniteHealth) {
            var background = GUI.skin.box.normal.background;
            GUI.skin.box.normal.background = Texture2D.whiteTexture;
            GUI.backgroundColor = Color.red;
            float healthPercentage = (float)health.CurrentHealth / health.maxHealth;
            int healthWidth = (int)(Screen.width * 0.05f);
            int healtHeight = (int)(Screen.height * 0.2f * healthPercentage);
            GUI.Box(new Rect(healthWidth, Screen.height - healthWidth - healtHeight,
                healthWidth, healtHeight), "");
            GUI.skin.box.normal.background = background;
        }

        if (currentRoom && currentRoom.Cleared) {
            DrawHUDText("Exit", Color.green, currentRoom.Exit ?
                currentRoom.Exit.transform.position : currentRoom.Bounds.center);
        } else if (currentEnemy) {
            Color enemyColor;
            if (enemyVisible) {
                enemyColor = Color.red;
            } else if (currentEnemy.Firing) {
                enemyColor = Color.yellow;
            } else {
                enemyColor = Color.black;
            }
            DrawHUDText("Enemy", enemyColor, currentEnemy.transform.position);
        }
    }

    /// <summary>
    /// Applies steering and jumping inputs onto the CharacterController.
    /// </summary>
    private void Move(Vector2 input, bool jumping) {
        Vector2 curSpeed = input * runningSpeed;
        float movementDirectionY = moveDirection.y;
        moveDirection = (transform.forward * curSpeed.y) + (transform.right * curSpeed.x);

        if (jumping && characterController.isGrounded)
        {
            moveDirection.y = jumpSpeed;
        }
        else
        {
            moveDirection.y = movementDirectionY;
        }

        if (!characterController.isGrounded)
        {
            moveDirection.y -= gravity * Time.fixedDeltaTime;
        }

        // Move the controller
        characterController.Move(moveDirection * Time.fixedDeltaTime);
    }

    /// <summary>
    /// Applies look inputs on the the associated Camera.
    /// </summary>
    private void Look(Vector2 input) {
        // Player and Camera rotation
        rotationX += input.y * lookSpeed;
        rotationX = Mathf.Clamp(rotationX, -lookPitchLimit, lookPitchLimit);
        playerCamera.transform.localRotation = Quaternion.Euler(rotationX, 0, 0);
        transform.rotation *= Quaternion.Euler(0, input.x * lookSpeed, 0);

        RaycastHit hit;
        Transform cameraTransform = playerCamera.transform;
        if (Physics.Raycast(
            cameraTransform.position, cameraTransform.forward, out hit, reticleMaxRange)) {
            reticleTarget = hit.collider.gameObject;
            reticleHit = hit.point;
        } else {
            reticleTarget = null;
            reticleHit = Vector3.zero;
        }
    }

    /// <summary>
    /// Requests that the associated weapon fire at the target under the reticle.
    /// </summary>
    private void Fire() {
        if (weapon) {
            if (reticleTarget) {
                weapon.Fire(reticleTarget, reticleHit);
            } else {
                weapon.Fire(null, playerCamera.transform.position +
                    playerCamera.transform.forward * reticleMaxRange);
            }
        }
    }

    /// <summary>
    /// Returns the room that contains the player, if any.
    /// </summary>
    private void UpdateRoom() {
        currentRoom = null;
        int nRooms = Room.Rooms.Count;
        Vector3 playerPosition = transform.position;
        for (int i = 0; i < nRooms; ++i) {
            if (Room.Rooms[i].Bounds.Contains(playerPosition)) {
                 currentRoom = Room.Rooms[i];
                 break;
            }
        }
    }

    /// <summary>
    /// Returns the best enemy, if any.
    /// </summary>
    private void UpdateEnemy(float maxDistance) {
        currentEnemy = null;
        enemyVisible = false;
        float bestScore = 0;
        int nEnemies = Enemy.Enemies.Count;
        Vector3 playerPosition = transform.position;
        Vector3 playerLook = playerCamera.transform.forward;
        for (int i = 0; i < nEnemies; ++i) {
            Enemy enemy = Enemy.Enemies[i];
            Vector3 toEnemy = enemy.transform.position - playerPosition;
            float distance = toEnemy.magnitude;
            if (distance <= maxDistance) {
                toEnemy /= distance;
                float facingScore = Vector3.Dot(toEnemy, playerLook);
                float distanceScore = 1f - (distance / maxDistance);
                float totalScore = facingScore + distanceScore;
                if (totalScore > bestScore) {
                    currentEnemy = enemy;
                    bestScore = totalScore;
                }
            }
        }
        if (currentEnemy) {
            Vector3 enemyDirection = currentEnemy.transform.position - TargetPos;
            float distanceToEnemy = enemyDirection.magnitude;
            enemyDirection /= distanceToEnemy;
            RaycastHit hit;
            enemyVisible = !Physics.Raycast(TargetPos, enemyDirection, out hit, distanceToEnemy) ||
                hit.collider.gameObject == currentEnemy.gameObject;
        }
    }

    /// <summary>
    /// Renders a screenspace label above a world space position;
    /// </summary>
    private void DrawHUDText(string text, Color color, Vector3 position) {
        position += Vector3.up;
        Vector3 screenCoords = Camera.main.WorldToScreenPoint(position);
        screenCoords.y = Screen.height - screenCoords.y;
        const float width = 50;
        const float height = 20;
        GUIStyle style = new GUIStyle
        {
            fontSize = 20,
            alignment = TextAnchor.MiddleCenter,
        };
        style.normal.textColor = color;
        GUI.color = Color.white;
        GUI.Label(new Rect(screenCoords.x - width * 0.5f, screenCoords.y - height * 0.5f,
            width, height), text, style);
    }
}
