/************************************************************************************

Copyright   :   Copyright 2014 Oculus VR, LLC. All Rights reserved.

Licensed under the Oculus VR Rift SDK License Version 3.2 (the "License");
you may not use the Oculus VR Rift SDK except in compliance with the License,
which is provided at the time of installation or download, or which
otherwise accompanies this software in either electronic or hard copy form.

You may obtain a copy of the License at

http://www.oculusvr.com/licenses/LICENSE-3.2

Unless required by applicable law or agreed to in writing, the Oculus VR SDK
distributed under the License is distributed on an "AS IS" BASIS,
WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
See the License for the specific language governing permissions and
limitations under the License.

************************************************************************************/

using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Controls the player's movement in virtual reality.
/// </summary>
[RequireComponent(typeof(CharacterController))]
public class OVRPlayerController : MonoBehaviour
{
	/// <summary>
	/// The rate acceleration during movement.
	/// </summary>
	public float Acceleration = 0.1f;

	/// <summary>
	/// The rate of damping on movement.
	/// </summary>
	public float Damping = 0.3f;

	/// <summary>
	/// The rate of additional damping when moving sideways or backwards.
	/// </summary>
	public float BackAndSideDampen = 0.5f;

	/// <summary>
	/// The force applied to the character when jumping.
	/// </summary>
	public float JumpForce = 0.3f;

	/// <summary>
	/// The rate of rotation when using a gamepad.
	/// </summary>
	public float RotationAmount = 1.5f;

	/// <summary>
	/// The rate of rotation when using the keyboard.
	/// </summary>
	public float RotationRatchet = 45.0f;

	/// <summary>
	/// The player's current rotation about the Y axis.
	/// </summary>
	private float YRotation = 0.0f;

	/// <summary>
	/// If true, tracking data from a child OVRCameraRig will update the direction of movement.
	/// </summary>
	public bool HmdRotatesY = true;

	/// <summary>
	/// Modifies the strength of gravity.
	/// </summary>
	public float GravityModifier = 0.379f;

	private float MoveScale = 1.0f;
	private Vector3 MoveThrottle = Vector3.zero;
	private float FallSpeed = 0.0f;	
	private OVRPose? InitialPose;
	
	/// <summary>
	/// If true, each OVRPlayerController will use the player's physical height.
	/// </summary>
	public bool useProfileHeight = true;

	protected CharacterController Controller = null;
	protected OVRCameraRig CameraController = null;

	private float MoveScaleMultiplier = 1.0f;
	private float RotationScaleMultiplier = 1.0f;
	private bool  SkipMouseRotation = false;
	private bool  HaltUpdateMovement = false;
	private bool prevHatLeft = false;
	private bool prevHatRight = false;
	private float SimulationRate = 60f;

	private Quaternion MovementDirection;

	void Awake()
	{
		Controller = gameObject.GetComponent<CharacterController>();

		if(Controller == null)
			Debug.LogWarning("OVRPlayerController: No CharacterController attached.");

		// We use OVRCameraRig to set rotations to cameras,
		// and to be influenced by rotation
		OVRCameraRig[] CameraControllers;
		CameraControllers = gameObject.GetComponentsInChildren<OVRCameraRig>();

		if(CameraControllers.Length == 0)
			Debug.LogWarning("OVRPlayerController: No OVRCameraRig attached.");
		else if (CameraControllers.Length > 1)
			Debug.LogWarning("OVRPlayerController: More then 1 OVRCameraRig attached.");
		else
			CameraController = CameraControllers[0];

		YRotation = transform.rotation.eulerAngles.y;
		MovementDirection = transform.rotation;

#if UNITY_ANDROID && !UNITY_EDITOR
		OVRManager.display.RecenteredPose += ResetOrientation;
#endif
	}

	protected virtual void Update()
	{
		if (useProfileHeight)
		{
			if (InitialPose == null)
			{
				InitialPose = new OVRPose() {
					position = CameraController.transform.localPosition,
					orientation = CameraController.transform.localRotation
				};
			}

			var p = CameraController.transform.localPosition;
			p.y = OVRManager.profile.eyeHeight - 0.5f * Controller.height;
			p.z = OVRManager.profile.eyeDepth;
			CameraController.transform.localPosition = p;
		}
		else if (InitialPose != null)
		{
			CameraController.transform.localPosition = InitialPose.Value.position;
			CameraController.transform.localRotation = InitialPose.Value.orientation;
			InitialPose = null;
		}

		UpdateMovement();

		Vector3 moveDirection = Vector3.zero;

		float motorDamp = (1.0f + (Damping * SimulationRate * Time.deltaTime));

		MoveThrottle.x /= motorDamp;
		MoveThrottle.y = (MoveThrottle.y > 0.0f) ? (MoveThrottle.y / motorDamp) : MoveThrottle.y;
		MoveThrottle.z /= motorDamp;

		moveDirection += MoveThrottle * SimulationRate * Time.deltaTime;
		moveDirection += 0.1f * SimulationRate * Time.deltaTime * (MovementDirection * Vector3.forward);

		Vector3 predictedXZ = Vector3.Scale((Controller.transform.localPosition + moveDirection), new Vector3(1, 0, 1));

		// Move contoller
		Controller.Move(moveDirection);

		Vector3 actualXZ = Vector3.Scale(Controller.transform.localPosition, new Vector3(1, 0, 1));

		if (predictedXZ != actualXZ)
			MoveThrottle += (actualXZ - predictedXZ) / (SimulationRate * Time.deltaTime);
	}

	public virtual void UpdateMovement()
	{
		if (HaltUpdateMovement)
			return;

		//bool pitchUp = Input.GetKey(KeyCode.W) || Input.GetKey(KeyCode.UpArrow);
		//bool moveLeft = Input.GetKey(KeyCode.A) || Input.GetKey(KeyCode.LeftArrow);
		//bool moveRight = Input.GetKey(KeyCode.D) || Input.GetKey(KeyCode.RightArrow);
		//bool moveBack = Input.GetKey(KeyCode.S) || Input.GetKey(KeyCode.DownArrow);

		//bool dpad_move = false;

		//if (OVRGamepadController.GPC_GetButton(OVRGamepadController.Button.Up))
		//{
		//	moveForward = true;
		//	dpad_move   = true;
		//
		//}
		//if (OVRGamepadController.GPC_GetButton(OVRGamepadController.Button.Down))
		//{
		//	moveBack  = true;
		//	dpad_move = true;
		//}

		MoveScale = 1.0f * SimulationRate * Time.deltaTime;

		// Compute this for key movement
		float moveInfluence = Acceleration * 0.1f * MoveScale * MoveScaleMultiplier;

		// Run!
		//if (dpad_move || Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift))
		//	moveInfluence *= 2.0f;
		//MoveThrottle +=  (transform.lossyScale.z * moveInfluence * Vector3.forward);
		//if (pitchUp)
		//if (moveBack)
		//	MoveThrottle +=  (transform.lossyScale.z * moveInfluence * BackAndSideDampen * Vector3.back);
		//if (moveLeft)
		//	MoveThrottle += (transform.lossyScale.x * moveInfluence * BackAndSideDampen * Vector3.left);
		//if (moveRight)
		//	MoveThrottle +=  (transform.lossyScale.x * moveInfluence * BackAndSideDampen * Vector3.right);

		bool curHatLeft = OVRGamepadController.GPC_GetButton(OVRGamepadController.Button.LeftShoulder);

		Vector3 euler = transform.rotation.eulerAngles;

		if (curHatLeft && !prevHatLeft)
			euler.y -= RotationRatchet;

		prevHatLeft = curHatLeft;

		bool curHatRight = OVRGamepadController.GPC_GetButton(OVRGamepadController.Button.RightShoulder);

		if(curHatRight && !prevHatRight)
			euler.y += RotationRatchet;

		prevHatRight = curHatRight;

		//Use keys to ratchet rotation
		Vector3 dirEuler = MovementDirection.eulerAngles;
		if (Input.GetKeyDown(KeyCode.A))
			dirEuler.y -= RotationRatchet;

		if (Input.GetKeyDown(KeyCode.D))
			dirEuler.y += RotationRatchet;
		MovementDirection = Quaternion.Euler (dirEuler);

		float rotateInfluence = SimulationRate * Time.deltaTime * RotationAmount * RotationScaleMultiplier;

		if (!SkipMouseRotation)
			euler.y += Input.GetAxis("Mouse X") * rotateInfluence * 3.25f;

		moveInfluence = SimulationRate * Time.deltaTime * Acceleration * 0.1f * MoveScale * MoveScaleMultiplier;

#if !UNITY_ANDROID // LeftTrigger not avail on Android game pad
		moveInfluence *= 1.0f + OVRGamepadController.GPC_GetAxis(OVRGamepadController.Axis.LeftTrigger);
#endif


		transform.rotation = Quaternion.Euler(euler);
	}

	/// <summary>
	/// Jump! Must be enabled manually.
	/// </summary>
	public bool Jump()
	{
		if (!Controller.isGrounded)
			return false;

		MoveThrottle += new Vector3(0, JumpForce, 0);

		return true;
	}

	/// <summary>
	/// Stop this instance.
	/// </summary>
	public void Stop()
	{
		Controller.Move(Vector3.zero);
		MoveThrottle = Vector3.zero;
		FallSpeed = 0.0f;
	}

	/// <summary>
	/// Gets the move scale multiplier.
	/// </summary>
	/// <param name="moveScaleMultiplier">Move scale multiplier.</param>
	public void GetMoveScaleMultiplier(ref float moveScaleMultiplier)
	{
		moveScaleMultiplier = MoveScaleMultiplier;
	}

	/// <summary>
	/// Sets the move scale multiplier.
	/// </summary>
	/// <param name="moveScaleMultiplier">Move scale multiplier.</param>
	public void SetMoveScaleMultiplier(float moveScaleMultiplier)
	{
		MoveScaleMultiplier = moveScaleMultiplier;
	}

	/// <summary>
	/// Gets the rotation scale multiplier.
	/// </summary>
	/// <param name="rotationScaleMultiplier">Rotation scale multiplier.</param>
	public void GetRotationScaleMultiplier(ref float rotationScaleMultiplier)
	{
		rotationScaleMultiplier = RotationScaleMultiplier;
	}

	/// <summary>
	/// Sets the rotation scale multiplier.
	/// </summary>
	/// <param name="rotationScaleMultiplier">Rotation scale multiplier.</param>
	public void SetRotationScaleMultiplier(float rotationScaleMultiplier)
	{
		RotationScaleMultiplier = rotationScaleMultiplier;
	}

	/// <summary>
	/// Gets the allow mouse rotation.
	/// </summary>
	/// <param name="skipMouseRotation">Allow mouse rotation.</param>
	public void GetSkipMouseRotation(ref bool skipMouseRotation)
	{
		skipMouseRotation = SkipMouseRotation;
	}

	/// <summary>
	/// Sets the allow mouse rotation.
	/// </summary>
	/// <param name="skipMouseRotation">If set to <c>true</c> allow mouse rotation.</param>
	public void SetSkipMouseRotation(bool skipMouseRotation)
	{
		SkipMouseRotation = skipMouseRotation;
	}

	/// <summary>
	/// Gets the halt update movement.
	/// </summary>
	/// <param name="haltUpdateMovement">Halt update movement.</param>
	public void GetHaltUpdateMovement(ref bool haltUpdateMovement)
	{
		haltUpdateMovement = HaltUpdateMovement;
	}

	/// <summary>
	/// Sets the halt update movement.
	/// </summary>
	/// <param name="haltUpdateMovement">If set to <c>true</c> halt update movement.</param>
	public void SetHaltUpdateMovement(bool haltUpdateMovement)
	{
		HaltUpdateMovement = haltUpdateMovement;
	}

	/// <summary>
	/// Resets the player look rotation when the device orientation is reset.
	/// </summary>
	public void ResetOrientation()
	{
		Vector3 euler = transform.rotation.eulerAngles;
		euler.y = YRotation;
		transform.rotation = Quaternion.Euler(euler);
	}
}

