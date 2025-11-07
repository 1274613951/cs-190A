using UnityEngine;
using Grpc.Net.Client;
using GrpcGreeterClient;
using Cysharp.Net.Http;
using static GrpcGreeterClient.Greeter;
using UnityEngine.Splines;
using Unity.Mathematics;


public class CarControl : MonoBehaviour
{
    [SerializeField] private UnityEngine.UI.Slider progressBar;
    private YetAnotherHttpHandler handler;
    private GrpcChannel channel;
    private GreeterClient client;

    [SerializeField] private MeshRenderer[] wheelMesh;
    [SerializeField] private WheelCollider[] wheelColliders;

    float h, v;

    // [SerializeField] private float maxAngle = 20;
    private float power = 2000;
    private float speed;
    private Rigidbody rb;
    [SerializeField] AnimationCurve turning;

    // used to store the initial position and rotation of the car
    private Vector3 initialPosition;
    private Quaternion initialRotation;

    private float rewardEarnedThisStep = 0f; // reward earned, zero at the beginning of each episode
    private bool episodeDone = false;

    private float splineProgress = 0f; // Value from 0 to 1
    private float lastSplineProgress = 0f; // For delta progress

    private float distanceLeft; //get the distance of the to the left and the right of the track

    [SerializeField] private SplineContainer progressSpline;

    [SerializeField] private int time_scale = 1; 
    [SerializeField] private bool useRandomSpawn = false;
    [SerializeField] private Transform[] spawnPoints;
    private float episodeTimer = 0f;

    private float xOffset = 0f;
    private float zOffset = 0f;

    private float accumulatedProgressReward; 
    private void Start()
    {
        episodeDone = false;
        Time.timeScale = time_scale;
        rb = GetComponent<Rigidbody>();
        initialPosition = spawnPoints[0].position;
        initialRotation = spawnPoints[0].rotation;
        handler = new YetAnotherHttpHandler() { Http2Only = true };
        channel = GrpcChannel.ForAddress("http://localhost:5078", new GrpcChannelOptions() { HttpHandler = handler });
        client = new Greeter.GreeterClient(channel);
        episodeTimer = 0f;
        rewardEarnedThisStep = 0f;
        accumulatedProgressReward = 0f;

    }

    private float updateTimer = 0f;
    private float updateInterval = 0.04f; 
    private float direction; 
    private void Update() {
        updateTimer += Time.deltaTime;
        episodeTimer += Time.deltaTime;
        if (updateTimer >= updateInterval) {
            GetResponseAsync();
            updateTimer = 0f;
        }
        speed = rb.velocity.magnitude;
        Turn();
        Move();
        UpdateWheel();
    }
    private void OnTriggerEnter(Collider other)
    { 

        if (other.CompareTag("PenaltyWall"))
        {
            rewardEarnedThisStep += -30f;
            episodeDone = true; 
        }
    }
    async void GetResponseAsync()
    {
        UpdateSplineProgress(); //update the spline progress, and reward added in the function
        // float direction = Vector3.SignedAngle(Vector3.forward, velocity, Vector3.up);
        if(episodeTimer >= 300f){
            episodeDone = true; // to indicate that the episode is over
            print("Episode time out. Resetting car.");
        }

        // float lateralVelocity = Vector3.Dot(rb.velocity, transform.right); // sideway motion
        // float slipPenalty = Mathf.Abs(lateralVelocity) * 1f; // tune multiplier
        // rewardEarnedThisStep -= slipPenalty;
        InputStatus request = new InputStatus {
            X = rb.position.x,
            Z = rb.position.z,
            Direction = direction,
            Speed = speed,
            Distance = rewardEarnedThisStep,
            Done = episodeDone,
            DistanceLeft = distanceLeft,
        };
        OutputAI response = await client.RequestInstructionAsync(request);
        rewardEarnedThisStep = 0f; // reset reward after sending to server
        if(response.Done == true){
            print("Done signal received. Resetting car.");
            rb.velocity = Vector3.zero;
            rb.angularVelocity = Vector3.zero;

            if (useRandomSpawn && spawnPoints.Length > 0)
            {
                int index = UnityEngine.Random.Range(0, spawnPoints.Length);
                rb.position = spawnPoints[index].position;
                rb.rotation = spawnPoints[index].rotation; 
            }
            else
            {
                rb.position = initialPosition;
                rb.rotation = initialRotation;
            }
            episodeTimer = 0f;
            episodeDone = false; 
            accumulatedProgressReward = 0f;
            rewardEarnedThisStep = 0f;



            // Force progress re-evaluation at the new location
            Ray carRay = new Ray(transform.position, Vector3.up);
            Vector3 localOrigin = progressSpline.transform.InverseTransformPoint(carRay.origin);
            Vector3 localDirection = progressSpline.transform.InverseTransformDirection(carRay.direction);
            Ray localRay = new Ray(localOrigin, localDirection);
            float3 nearestPointLocal;
            float t;
            SplineUtility.GetNearestPoint(
                progressSpline.Spline, 
                localRay, 
                out nearestPointLocal, 
                out t
            );
            // Update both progress values to the current location
            splineProgress = t;
            lastSplineProgress = t;

            
            return;
        }
        xOffset = response.Action[0]; // this controls the velosity 
        zOffset = response.Action[1]; // this controls the steering
    }
    private void Turn(){
        float turningAngle = zOffset * turning.Evaluate(speed);
        for(int i = 0; i < 2; i++){
            wheelColliders[i].steerAngle = turningAngle;
        }
    }
    
    private void UpdateSplineProgress()
    {
        Ray carRay = new Ray(transform.position, Vector3.up);
        Vector3 localOrigin = progressSpline.transform.InverseTransformPoint(carRay.origin);
        Vector3 localDirection = progressSpline.transform.InverseTransformDirection(carRay.direction);
        Ray localRay = new Ray(localOrigin, localDirection);
        float3 nearestPointLocal;
        float t;
        SplineUtility.GetNearestPoint(
            progressSpline.Spline, 
            localRay, 
            out nearestPointLocal, 
            out t
        );

        // Progress tracking
        splineProgress = t; 
        float deltaProgress = splineProgress - lastSplineProgress;
        
        // Handle loop-around
        if (deltaProgress < -0.9f) deltaProgress += 1f; 
        if (deltaProgress > 0.9f) deltaProgress -= 1f;

        accumulatedProgressReward += deltaProgress * 100f; // Reward for progress and punishment for going backward

        rewardEarnedThisStep += accumulatedProgressReward; //reward for going forward 

        if (deltaProgress <= 0f)
        {
            rewardEarnedThisStep = -1; 
        }
    
        lastSplineProgress = splineProgress; 

        // Update UI Progress Bar
        if (progressBar != null){
            progressBar.value = splineProgress;
        }


        Vector3 leftNearestPointWorld = progressSpline.transform.TransformPoint(nearestPointLocal);

        distanceLeft = Vector3.Distance(transform.position, leftNearestPointWorld);
        
        // Draw a debug line from the car to the nearest spline point
        float3 tangentLocal = SplineUtility.EvaluateTangent(progressSpline.Spline, t);
        Vector3 tangentWorld = progressSpline.transform.TransformDirection((Vector3)tangentLocal);
        Vector3 carForward = transform.forward;
        direction = Vector3.SignedAngle(carForward, tangentWorld, Vector3.up);
        Debug.DrawLine(transform.position, leftNearestPointWorld, Color.green, 100f);
    }

    private void Move(){
        for(int i = 2; i < 4; i++){
            // wheelColliders[i].motorTorque = power * v;
            wheelColliders[i].motorTorque = power * xOffset;
        }
    }

    private void UpdateWheel(){
        for(int i = 0; i < 4; i++){
            Quaternion quaternion;
            Vector3 position;
            wheelColliders[i].GetWorldPose(out position, out quaternion);
            wheelMesh[i].transform.position = position;
            wheelMesh[i].transform.rotation = quaternion;
        }
    }
}