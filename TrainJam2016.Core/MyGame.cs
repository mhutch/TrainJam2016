using System;
using Urho;
using Urho.Physics;
using Urho.Resources;

namespace TrainJam2016
{
    public class MyGame : Application
    {
        public const string GameID = "TrainJam2016";
        public const string GameName = "Train Jam 2016";
        public const string GameCopyright = "Copyright © 2016 Mikayla Hutchinson";

        const float TouchSensitivity = 2;
        const float CameraDistance = 30.0f;

        Vehicle vehicle;
        Scene scene;

        public Node CameraNode { get; private set; }

        public MyGame() : base(new ApplicationOptions("Data") { })
        {
        }

        protected override void Start()
        {
            InitTouchInput();

            Input.SubscribeToKeyDown(HandleKeyDown);
        
            CreateScene();

            CreateVehicle();

            SubscribeToEvents();

            CreateConsoleAndDebugHud();
        }

        void HandleKeyDown(KeyDownEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Esc:
                    Engine.Exit();
                    return;
                case Key.F1:
                    console.Toggle();
                    return;
                case Key.F2:
                    debugHud.ToggleAll();
                    return;
            }
        }

        void SubscribeToEvents()
        {
            Engine.SubscribeToPostUpdate(args =>
            {
                if (vehicle == null)
                    return;

                Node vehicleNode = vehicle.Node;

                // Physics update has completed. Position camera behind vehicle
                Quaternion dir = vehicleRotation;
                dir = dir * Quaternion.FromAxisAngle(Vector3.UnitY, vehicle.Controls.Yaw);
                dir = dir * Quaternion.FromAxisAngle(Vector3.UnitX, vehicle.Controls.Pitch);

                Vector3 cameraTargetPos = vehicleNode.Position - (dir * new Vector3(0.0f, 0.0f, CameraDistance));
                Vector3 cameraStartPos = vehicleNode.Position;

                // Raycast camera against static objects (physics collision mask 2)
                // and move it closer to the vehicle if something in between
                Ray cameraRay = new Ray(cameraStartPos, cameraTargetPos - cameraStartPos);
                float cameraRayLength = (cameraTargetPos - cameraStartPos).Length;
                var result = new PhysicsRaycastResult();
                scene.GetComponent<PhysicsWorld>().RaycastSingleNoCrash(ref result, cameraRay, cameraRayLength, 2);

                if (result.Body != null)
                {
                    cameraTargetPos = cameraStartPos + cameraRay.Direction * (result.Distance - 0.5f);
                }

                CameraNode.Position = cameraTargetPos;
                CameraNode.Rotation = dir;
            });

            scene.GetComponent<PhysicsWorld>().SubscribeToPhysicsPreStep(args => vehicle?.FixedUpdate(args.TimeStep));
        }

        protected override void OnUpdate(float timeStep)
        {
            Input input = Input;

            if (vehicle != null)
            {
                // Get movement controls and assign them to the vehicle component. If UI has a focused element, clear controls
                if (UI.FocusElement == null)
                {
                    vehicle.Controls.Set(Vehicle.CtrlForward, input.GetKeyDown(Key.W) || input.GetKeyDown(Key.Up));
                    vehicle.Controls.Set(Vehicle.CtrlBack, input.GetKeyDown(Key.S) || input.GetKeyDown(Key.Down));
                    vehicle.Controls.Set(Vehicle.CtrlLeft, input.GetKeyDown(Key.A) || input.GetKeyDown(Key.Left));
                    vehicle.Controls.Set(Vehicle.CtrlRight, input.GetKeyDown(Key.D) || input.GetKeyDown(Key.Right));

                    // Add yaw & pitch from the mouse motion or touch input. Used only for the camera, does not affect motion
                    if (TouchEnabled)
                    {
                        for (uint i = 0; i < input.NumTouches; ++i)
                        {
                            TouchState state = input.GetTouch(i);
                            Camera camera = CameraNode.GetComponent<Camera>();
                            if (camera == null)
                                return;

                            var graphics = Graphics;
                            vehicle.Controls.Yaw += TouchSensitivity * camera.Fov / graphics.Height * state.Delta.X;
                            vehicle.Controls.Pitch += TouchSensitivity * camera.Fov / graphics.Height * state.Delta.Y;
                        }
                    }
                    else
                    {
                        vehicle.Controls.Yaw += (float)input.MouseMoveX * Vehicle.YawSensitivity;
                        vehicle.Controls.Pitch += (float)input.MouseMoveY * Vehicle.YawSensitivity;
                    }
                    // Limit pitch
                    vehicle.Controls.Pitch = MathHelper.Clamp(vehicle.Controls.Pitch, 0.0f, 80.0f);
                }
                else
                    vehicle.Controls.Set(Vehicle.CtrlForward | Vehicle.CtrlBack | Vehicle.CtrlLeft | Vehicle.CtrlRight, false);
            }
        }

        void CreateVehicle()
        {
            Node vehicleNode = scene.CreateChild("Vehicle");
            vehicleNode.Position = (new Vector3(0.0f, 5.0f, 0.0f));

            // Create the vehicle logic component
            vehicle = new Vehicle();
            vehicleNode.AddComponent(vehicle);
            // Create the rendering and physics components
            vehicle.Init();
        }


        void CreateScene()
        {
            var cache = ResourceCache;

            scene = new Scene();

            // Create scene subsystem components
            scene.CreateComponent<Octree>();
            scene.CreateComponent<PhysicsWorld>();

            // Create camera and define viewport. We will be doing load / save, so it's convenient to create the camera outside the scene,
            // so that it won't be destroyed and recreated, and we don't have to redefine the viewport on load
            CameraNode = new Node();
            Camera camera = CameraNode.CreateComponent<Camera>();
            camera.FarClip = 500.0f;
            Renderer.SetViewport(0, new Viewport(Context, scene, camera, null));

            // Create static scene content. First create a zone for ambient lighting and fog control
            Node zoneNode = scene.CreateChild("Zone");
            Zone zone = zoneNode.CreateComponent<Zone>();
            zone.AmbientColor = new Color(0.15f, 0.15f, 0.15f);
            zone.FogColor = new Color(0.5f, 0.5f, 0.7f);
            zone.FogStart = 300.0f;
            zone.FogEnd = 500.0f;
            zone.SetBoundingBox(new BoundingBox(-2000.0f, 2000.0f));

            // Create a directional light with cascaded shadow mapping
            Node lightNode = scene.CreateChild("DirectionalLight");
            lightNode.SetDirection(new Vector3(0.3f, -0.5f, 0.425f));
            Light light = lightNode.CreateComponent<Light>();
            light.LightType = LightType.Directional;
            light.CastShadows = true;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            light.ShadowCascade = new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f);
            light.SpecularIntensity = 0.5f;

            // Create heightmap terrain with collision
            Node terrainNode = scene.CreateChild("Terrain");
            terrainNode.Position = (Vector3.Zero);
            Terrain terrain = terrainNode.CreateComponent<Terrain>();
            terrain.PatchSize = 64;
            terrain.Spacing = new Vector3(2.0f, 0.1f, 2.0f); // Spacing between vertices and vertical resolution of the height map
            terrain.Smoothing = true;
            terrain.SetHeightMap(cache.GetImage("Textures/HeightMap.png"));
            terrain.Material = cache.GetMaterial("Materials/Terrain.xml");
            // The terrain consists of large triangles, which fits well for occlusion rendering, as a hill can occlude all
            // terrain patches and other objects behind it
            terrain.Occluder = true;

            RigidBody body = terrainNode.CreateComponent<RigidBody>();
            body.CollisionLayer = 2; // Use layer bitmask 2 for static geometry
            CollisionShape shape = terrainNode.CreateComponent<CollisionShape>();
            shape.SetTerrain(0);

            // Create 1000 mushrooms in the terrain. Always face outward along the terrain normal
            const uint numMushrooms = 1000;
            for (uint i = 0; i < numMushrooms; ++i)
            {
                Node objectNode = scene.CreateChild("Mushroom");
                Vector3 position = new Vector3(NextRandom(2000.0f) - 1000.0f, 0.0f, NextRandom(2000.0f) - 1000.0f);
                position.Y = terrain.GetHeight(position) - 0.1f;
                objectNode.Position = (position);
                // Create a rotation quaternion from up vector to terrain normal
                objectNode.Rotation = Quaternion.FromRotationTo(Vector3.UnitY, terrain.GetNormal(position));
                objectNode.SetScale(3.0f);
                StaticModel sm = objectNode.CreateComponent<StaticModel>();
                sm.Model = (cache.GetModel("Models/Mushroom.mdl"));
                sm.SetMaterial(cache.GetMaterial("Materials/Mushroom.xml"));
                sm.CastShadows = true;

                body = objectNode.CreateComponent<RigidBody>();
                body.CollisionLayer = 2;
                shape = objectNode.CreateComponent<CollisionShape>();
                shape.SetTriangleMesh(sm.Model, 0, Vector3.One, Vector3.Zero, Quaternion.Identity);
            }
        }

        static readonly Random random = new Random();
        /// Return a random float between 0.0 (inclusive) and 1.0 (exclusive.)
        public static float NextRandom() { return (float)random.NextDouble(); }
        /// Return a random float between 0.0 and range, inclusive from both ends.
        public static float NextRandom(float range) { return (float)random.NextDouble() * range; }
        /// Return a random float between min and max, inclusive from both ends.
        public static float NextRandom(float min, float max) { return (float)((random.NextDouble() * (max - min)) + min); }
        /// Return a random integer between min and max - 1.
        public static int NextRandom(int min, int max) { return random.Next(min, max); }


        /// <summary>
        /// Joystick XML layout for mobile platforms
        /// </summary>
        string JoystickLayoutPatch => string.Empty;


        bool TouchEnabled { get; set; }

        void InitTouchInput()
        {
            if (Platform != Platforms.Android &&
                Platform != Platforms.iOS &&
                !Options.TouchEmulation)
            {
                return;
            }

            TouchEnabled = true;
            var layout = ResourceCache.GetXmlFile("UI/ScreenJoystick_Samples.xml");
            if (!string.IsNullOrEmpty(JoystickLayoutPatch))
            {
                var patchXmlFile = new XmlFile();
                patchXmlFile.FromString(JoystickLayoutPatch);
                layout.Patch(patchXmlFile);
            }
            var screenJoystickIndex = Input.AddScreenJoystick(layout, ResourceCache.GetXmlFile("UI/DefaultStyle.xml"));
            Input.SetScreenJoystickVisible(screenJoystickIndex, true);
        }

        UrhoConsole console;
        DebugHud debugHud;

        void CreateConsoleAndDebugHud()
        {
            var cache = ResourceCache;

            var xml = cache.GetXmlFile("UI/DefaultStyle.xml");
            console = Engine.CreateConsole();
            console.DefaultStyle = xml;
            console.Background.Opacity = 0.8f;

            debugHud = Engine.CreateDebugHud();
            debugHud.DefaultStyle = xml;
        }
   }
}
