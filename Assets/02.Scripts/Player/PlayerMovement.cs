using System;
using System.Collections;
using UnityEngine;

namespace _12.Tests.PlayerDev
{
    public class PlayerInput
    {
        public float moveX;
        public float moveZ;
        public bool jump;
        public bool run;
        
        public void Clear()
        {
            moveX = 0f;
            moveZ = 0f;
            jump = false;
            run = false;
        } 
    }
    
    public class PlayerMovement : MonoBehaviour
    {
        [Range(1f, 20f)]
        [SerializeField] private float _movementSpeed;
        
        [Tooltip("run multiplier of the movement speed")]
        [Range(1f, 20f)]
        [SerializeField] private float _runMultiplier;
        
        [SerializeField] private float _gravity = -9.81f;
        [Range(0f, 3f)]
        [SerializeField] private float _jumpHeight;
        
        [Tooltip("time taken to rotate when the direction of movement is changed")]
        [Range(0f, 0.1f)]
        [SerializeField] private float _smoothTime = 0.1f;

        private CharacterController characterController;
        private Transform playerTransform;
        private Animator playerAnimator;
        private Renderer playerMeshRenderer;
        private Vector3 _controllerVelocity;
        private Vector3 _lastStablePosition;
        
        private PlayerInput input;
        
        private IEnumerator jumpCheckGroundAvoider; 
        private IEnumerator JumpCheckGroundAvoider()
        {
            yield return new WaitForFixedUpdate();
            playerAnimator.SetBool("Jump", false);
        }
        
        private int runLayer = 1;
        private float runLayerWeight = 0f;
        private float runTransitionSpeed = 3f;

        private bool immune = false;

        // Start is called before the first frame update
        private void Awake()
        {
            input = new PlayerInput();
            characterController = GetComponent<CharacterController>();
            playerTransform = transform.Find("Player");
            playerAnimator = playerTransform.GetComponent<Animator>();
            playerMeshRenderer = playerTransform.GetComponentInChildren<Renderer>();
        }

        // Update is called once per frame
        private void Update()
        {
            GetInputs();
            ControlPlayer();
        }

        private void GetInputs()
        {
            input.Clear();
            
            // get the movement input
            input.moveX = Input.GetAxis("Horizontal");
            input.moveZ = Input.GetAxis("Vertical");
            input.jump = Input.GetButton("Jump");
            input.run = Input.GetKey(KeyCode.LeftShift);
        }

        private void ControlPlayer()
        {
            // stops the y velocity when player is on the ground and the velocity has reached 0
            if (characterController.isGrounded && _controllerVelocity.y < 0)
            {
                _controllerVelocity.y = 0;
            }

            CheckStable();
            
            // moves the controller in the desired direction on the x- and z-axis
            Vector3 movement = transform.right * input.moveX + transform.forward * input.moveZ;
            characterController.Move(movement * (_movementSpeed * Time.deltaTime));

            // the controller is able to run
            if (input.run)
            {
                characterController.Move(movement * (Time.deltaTime * _runMultiplier));

                if(playerAnimator.GetBool("Grounded"))
                {
                    runLayerWeight = Mathf.MoveTowards(runLayerWeight, 1f, Time.deltaTime * runTransitionSpeed);
                }
            }else{
                runLayerWeight = Mathf.MoveTowards(runLayerWeight, 0f, Time.deltaTime * runTransitionSpeed);
            }
            playerAnimator.SetLayerWeight(runLayer, runLayerWeight);
            
            // set player's forward same as moving direction
            float currentVelocity = movement.magnitude;
            playerAnimator.SetBool("Moving", currentVelocity > 0);
            if (currentVelocity > 0)
            {
                float targetAngle = Mathf.Atan2(input.moveX, input.moveZ) * Mathf.Rad2Deg - 90;
                float angle = Mathf.SmoothDampAngle(playerTransform.eulerAngles.y, targetAngle, ref currentVelocity, _smoothTime);
                playerTransform.rotation = Quaternion.Euler(0, angle, 0);
            }
                
            // gravity affects the controller on the y-axis
            _controllerVelocity.y += _gravity * Time.deltaTime;

            // moves the controller on the y-axis
            characterController.Move(_controllerVelocity * Time.deltaTime);
            
            if (characterController.isGrounded) playerAnimator.SetBool("Grounded", true);

            // the controller is able to jump when on the ground
            if (input.jump && characterController.isGrounded)
            {
                playerAnimator.SetBool("Jump", true);
                playerAnimator.SetBool("Grounded", false);
                _controllerVelocity.y = Mathf.Sqrt(_jumpHeight * -2f * _gravity);
                
                if(jumpCheckGroundAvoider != null)
                {
                    StopCoroutine(jumpCheckGroundAvoider);
                }
                jumpCheckGroundAvoider = JumpCheckGroundAvoider();
                StartCoroutine(jumpCheckGroundAvoider);
            }
        }

        // Convert the direction of movement to avoid entering a impassible area
        private void CheckStable()
        {
            if (immune) return;
            
            Debug.DrawRay(_lastStablePosition, Vector3.up, Color.blue);
            if (!characterController.isGrounded) return;
            
            // Restricted distance for impassable areas
            float checkDistance = characterController.radius * 10f;
            // Inspection resolution
            int numStep = 20;
            
            for (int i = 0; i < numStep; i++)
            {
                float checkAngle = 360f * i / numStep;
                Vector3 checkDirection = Quaternion.AngleAxis(checkAngle, Vector3.up) * Vector3.forward;
                if (!CheckPassable(transform.position + checkDirection * checkDistance)) return;
            }

            _lastStablePosition = transform.position + Vector3.up * 1f;
        }

        // Check if a location is passable
        private bool CheckPassable(Vector3 position)
        {
            // Subject to modification based on development
            Ray ray = new Ray(position + Vector3.up * 1f, Vector3.down);
            if (!Physics.Raycast(ray, out RaycastHit hit)) return false;
            return !hit.transform.CompareTag("Water");
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            if (hit.gameObject.CompareTag("Water"))
            {
                RespawnPlayer();
            }
        }

        public void RespawnPlayer()
        {
            if (immune) return;
            playerMeshRenderer.enabled = false;
            immune = true;
            Input.ResetInputAxes();
            
            StartCoroutine((new[] {
                MoveTowardsTarget(),
                BlinkPlayer(),
            }).GetEnumerator());
        }

        private IEnumerator MoveTowardsTarget()
        {
            float delayTime = 1f;
            float elapsedTime = 0f;
            
            Vector3 offset = _lastStablePosition - transform.position;
            offset.y = 0f;
            float moveSpeed = offset.magnitude / delayTime;
            while (elapsedTime < delayTime)
            {
                elapsedTime += Time.deltaTime;
                offset = _lastStablePosition - transform.position;
                offset.y = 0f;
                characterController.Move(offset.normalized * (moveSpeed * Time.deltaTime));
                yield return null;
            }
        }
        
        private IEnumerator BlinkPlayer()
        {
            for (int i=0; i<5; i++)
            {
                playerMeshRenderer.enabled = false;
                yield return new WaitForSeconds(0.1f);
                playerMeshRenderer.enabled = true;
                yield return new WaitForSeconds(0.1f);
            }

            immune = false;
        }
    }
}
