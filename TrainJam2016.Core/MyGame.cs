using System;
using Urho;
using Urho.Physics;
using Urho.Resources;
using Urho.Shapes;
using Urho.Gui;
using System.Threading.Tasks;

namespace TrainJam2016
{
    public class MyGame : Application
    {
        public const string GameID = "TrainJam2016";
        public const string GameName = "Train Jam 2016";
        public const string GameCopyright = "Copyright © 2016 Mikayla Hutchinson";

        const float TouchSensitivity = 2;

        Vehicle vehicle;
        Scene scene;
        Terrain terrain;

        public Node CameraNode { get; private set; }

        public MyGame() : base(new ApplicationOptions("Data") { })
        {
        }

        protected override void Start()
        {
            Graphics.WindowTitle = GameName;

            InitTouchInput();

            Input.SubscribeToKeyDown(HandleKeyDown);

            CreateScene();

            CreateVehicle();

            SubscribeToEvents();

            CreateConsoleAndDebugHud();

            CreateUI();

            RunMessages("Stack as many blocks as you can!");
        }

        bool hadFirstInput = false;

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
                case Key.C:
                    cameraSnapping = !cameraSnapping;
                    return;
            }
        }

        const float cameraDistance = 10.0f;
        const float cameraDegrees = 10.0f;

        bool cameraSnapping = true;
        const float cameraResetTimeout = 0.2f;
        const float cameraSnapRate = 2f;

        const float cameraDegreesPerBlock = 3f;
        const float cameraDistancePerBlock = 3f;

        float cameraBlocksAdjustment;
        float cameraBlocksAdjustRate = 1f;

        void SubscribeToEvents()
        {
            Engine.SubscribeToPostUpdate(args =>
            {
                if (vehicle == null)
                    return;

                Node vehicleNode = vehicle.Node;
                Quaternion vehicleRotation = vehicleNode.Rotation;

                cameraBlocksAdjustment = MathHelper.Lerp (cameraBlocksAdjustment, liveBlocks, cameraBlocksAdjustRate * args.TimeStep);

                float adjustedDegrees = cameraDegrees + cameraBlocksAdjustment * cameraDegreesPerBlock;
                float adjustedDistance = cameraDistance + cameraBlocksAdjustment * cameraDistancePerBlock;

                timeSinceLastMouse += args.TimeStep;
                if (cameraSnapping && timeSinceLastMouse > cameraResetTimeout)
                {
                    var snap = args.TimeStep * cameraSnapRate;
                    vehicle.Controls.Yaw -= vehicle.Controls.Yaw * Math.Min (1f, snap);
                    vehicle.Controls.Pitch += (adjustedDegrees - vehicle.Controls.Pitch) * snap;// 0.95f;
                }

                // Physics update has completed. Position camera behind vehicle
                // Start with the vehcle's heading. Using YawAngle then FromAxisAngle sometimes starts going wrong way?
                var dir = vehicleNode.Rotation;
                dir.X = 0f;
                dir.Z = 0f;
                dir.Normalize();

                //add in the mouse/touch yaw and pitch
                dir = dir * Quaternion.FromAxisAngle(Vector3.UnitY, vehicle.Controls.Yaw);
                dir = dir * Quaternion.FromAxisAngle(Vector3.UnitX, vehicle.Controls.Pitch);

                Vector3 cameraTargetPos = vehicleNode.Position - (dir * new Vector3(0.0f, 0.0f, adjustedDistance));
                Vector3 cameraStartPos = vehicleNode.Position;

                // Raycast camera against static objects (physics collision mask 2)
                // and move it closer to the vehicle if something in between
                Ray cameraRay = new Ray(cameraStartPos, cameraTargetPos - cameraStartPos);
                float cameraRayLength = (cameraTargetPos - cameraStartPos).Length;
                var result = new PhysicsRaycastResult();
                scene.GetComponent<PhysicsWorld>().RaycastSingleNoCrash(
                    ref result, cameraRay, cameraRayLength,
                    CollisionLayer.Static | CollisionLayer.Terrain);

                if (result.Body != null)
                {
                    cameraTargetPos = cameraStartPos + cameraRay.Direction * (result.Distance - 0.5f);
                }

                CameraNode.Position = cameraTargetPos;
                CameraNode.Rotation = dir;
            });

            scene.GetComponent<PhysicsWorld>().SubscribeToPhysicsPreStep(args => vehicle?.FixedUpdate(args.TimeStep));
        }

        float timeSinceLastMouse;

        protected override void OnUpdate(float timeStep)
        {
            Input input = Input;

            if (vehicle != null)
            {
                // Get movement controls and assign them to the vehicle component. If UI has a focused element, clear controls
                if (UI.FocusElement == null)
                {
                    var forward = input.GetKeyDown(Key.W) || input.GetKeyDown(Key.Up);
                    var back    = input.GetKeyDown(Key.S) || input.GetKeyDown(Key.Down);
                    var left    = input.GetKeyDown(Key.A) || input.GetKeyDown(Key.Left);
                    var right  = input.GetKeyDown(Key.D) || input.GetKeyDown(Key.Right);
                    vehicle.Controls.Set(Vehicle.CtrlForward, forward);
                    vehicle.Controls.Set(Vehicle.CtrlBack, back);
                    vehicle.Controls.Set(Vehicle.CtrlLeft, left);
                    vehicle.Controls.Set(Vehicle.CtrlRight, right);

                    if (!hadFirstInput && (forward || back || left || right))
                    {
                        hadFirstInput = true;
                        RunMessages("");
                    }

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
                        if (input.MouseMoveY != 0 || input.MouseMoveX != 0)
                        {
                            timeSinceLastMouse = 0;
                        }

                        vehicle.Controls.Yaw += (float)input.MouseMoveX * Vehicle.YawSensitivity;
                        vehicle.Controls.Pitch += (float)input.MouseMoveY * Vehicle.YawSensitivity;
                    }
                    // Limit pitch
                    vehicle.Controls.Pitch = MathHelper.Clamp(vehicle.Controls.Pitch, 10.0f, 60.0f);
                    vehicle.Controls.Yaw = MathHelper.Clamp(vehicle.Controls.Yaw, -80f, 80.0f);
                }
                else
                {
                    vehicle.Controls.Set(Vehicle.CtrlForward | Vehicle.CtrlBack | Vehicle.CtrlLeft | Vehicle.CtrlRight, false);
                }
            }
        }

        void CreateVehicle()
        {
            Node vehicleNode = scene.CreateChild("Vehicle");

            var position = new Vector3(0.0f, 5.0f, 0.0f);
            position.Y = terrain.GetHeight(position) + 2f;
            vehicleNode.Position = position;

            // Create the vehicle logic component
            vehicle = new Vehicle();
            vehicleNode.AddComponent(vehicle);
            // Create the rendering and physics components
            vehicle.Init();

            //stop blocks sliding off too easily
            vehicle.hullBody.Friction = 1f;
        }


        void CreateScene()
        {
            var cache = ResourceCache;

            scene = new Scene();

            // Create scene subsystem components
            scene.CreateComponent<Octree>();
            var physicsWorld = scene.CreateComponent<PhysicsWorld>();

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
            light.Color = Color.Cyan;
            light.CastShadows = true;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            light.ShadowCascade = new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f);
            light.SpecularIntensity = 0.5f;

            lightNode = scene.CreateChild("DirectionalLight");
            lightNode.SetDirection(new Vector3(-0.3f, -0.5f, -0.425f));
            light = lightNode.CreateComponent<Light>();
            light.LightType = LightType.Directional;
            light.Color = Color.Magenta;
            light.CastShadows = false;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            light.SpecularIntensity = 0.5f;

            // Create heightmap terrain with collision
            Node terrainNode = scene.CreateChild("Terrain");
            terrainNode.Position = (Vector3.Zero);
            terrain = terrainNode.CreateComponent<Terrain>();
            terrain.PatchSize = 64;
            terrain.Spacing = new Vector3(2.0f, 0.1f, 2.0f); // Spacing between vertices and vertical resolution of the height map
            terrain.Smoothing = true;
            terrain.SetHeightMap(cache.GetImage(Assets.Textures.HeightMap));
            terrain.Material = cache.GetMaterial(Assets.Materials.Terrain);
            // The terrain consists of large triangles, which fits well for occlusion rendering, as a hill can occlude all
            // terrain patches and other objects behind it
            terrain.Occluder = true;

            RigidBody body = terrainNode.CreateComponent<RigidBody>();
            body.CollisionLayer = CollisionLayer.Terrain;
            CollisionShape shape = terrainNode.CreateComponent<CollisionShape>();
            shape.SetTerrain(0);

            SpawnObstacles(cache, terrain);
            SpawnPickups(cache, terrain);

            physicsWorld.SubscribeToPhysicsCollision(HandlePhysicsCollision);
        }

        void HandlePhysicsCollision(PhysicsCollisionEventArgs args)
        {
            var layerA = args.BodyA.CollisionLayer;
            var layerB = args.BodyB.CollisionLayer;
            var layers = layerA | layerB;

            if (layers == (CollisionLayer.Pickup | CollisionLayer.Vehicle))
            {
                var node = (layerA == CollisionLayer.Pickup) ? args.NodeA : args.NodeB;
                node.GetComponent<RigidBody>().Enabled = false;
                ScaleAndDisappear(node, 0.5f, 1.5f);
                SpawnStackingBlock();
                return;
            }

            if (layers == (CollisionLayer.Block | CollisionLayer.Terrain))
            {
                var node = (layerA == CollisionLayer.Block) ? args.NodeA : args.NodeB;
                node.GetComponent<RigidBody>().CollisionMask ^= CollisionLayer.Terrain;
                ScaleAndDisappear(node, 0.5f, 0.2f);
                UpdateCountLabel(-1);
                return;
            }
        }

        public static async void ScaleAndDisappear(Node node, float duration, float scale)
        {
            await node.RunActionsAsync(new Urho.Actions.Parallel (
                new Urho.Actions.ScaleBy(duration, scale),
                new Urho.Actions.FadeOut (duration)
            ));
            node.Remove();
        }

        const float minBlockSpawnDelay = 1f;
        float lastBlockSpawnTime;

        void SpawnStackingBlock()
        {
            //FIXME: collision misbehaves when blocks are added too quickly
            var time = Time.ElapsedTime;
            if ((time - lastBlockSpawnTime) < minBlockSpawnDelay)
            {
                return;
            }
            lastBlockSpawnTime = time;

            UpdateCountLabel(1);

            Node node = scene.CreateChild("StackingBlock");
            node.Scale = new Vector3(3f, 1f, 3f);
            node.Rotation = vehicle.Node.Rotation;

            var box = node.CreateComponent<Box>();
            box.CastShadows = true;

            var body = node.CreateComponent<RigidBody>();
            body.CollisionLayer = CollisionLayer.Block;
            body.Mass = 4f;
            body.Friction = 100f;
            body.Restitution = 0.1f;
            body.LinearDamping = vehicle.hullBody.LinearDamping;
            body.SetLinearVelocity(vehicle.hullBody.LinearVelocity);

            var shape = node.CreateComponent<CollisionShape>();
            shape.SetBox(Vector3.One, Vector3.Zero, Quaternion.Identity);

            var pos = vehicle.Node.Position;

            var result = new PhysicsRaycastResult();
            float cameraRayLength = 100;
            var cameraRayFrom = new Vector3(pos.X, pos.Y + 100, pos.Z);
            var cameraRayDirection = -Vector3.UnitY;
            var cameraRay = new Ray(cameraRayFrom, cameraRayDirection);
            scene.GetComponent<PhysicsWorld>().RaycastSingleNoCrash(ref result, cameraRay, cameraRayLength, uint.MaxValue);

            pos.Y += 3f;
            if (result.Body != null)
            {
                pos.Y += cameraRayLength - result.Distance;
            }

            node.Position = pos;

            node.RunActionsAsync(new Urho.Actions.FadeIn(0.2f));
        }

        int liveBlocks;

        void UpdateCountLabel(int delta)
        {
            liveBlocks += delta;

            if (liveBlocks == 0)
            {
                RunMessages("Awwwwww!", "Try again!", "");
                return;
            }
            if (liveBlocks == 1 && delta == 1)
            {
                RunMessages("Good start!", "");
                return;
            }
            if (liveBlocks == 1 && delta == -1)
            {
                RunMessages("Almost back where you started...", "");
                return;
            }
            RunMessages ($"{liveBlocks} blocks", "");
        }

        TaskCompletionSource<bool> nextMessage;
        async void RunMessages(params string[] messages)
        {
            if (nextMessage != null)
                nextMessage.TrySetResult (false);
            var tcs = nextMessage = new TaskCompletionSource<bool>();

            foreach (var m in messages)
            {
                countLabel.Value = m;
                await Task.WhenAny(Task.Delay(2000), tcs.Task);
                if (tcs.Task.IsCompleted)
                    return;
            }
            tcs.TrySetResult(true);
        }

        void SpawnObstacles(ResourceCache cache, Terrain terrain)
        {
            // Create 1000 mushrooms in the terrain. Always face outward along the terrain normal
            const uint count = 1000;
            for (uint i = 0; i < count; ++i)
            {
                Node objectNode = scene.CreateChild("Mushroom");
                Vector3 position = new Vector3(NextRandom(2000.0f) - 1000.0f, 0.0f, NextRandom(2000.0f) - 1000.0f);
                position.Y = terrain.GetHeight(position) - 0.1f;
                objectNode.Position = (position);
                // Create a rotation quaternion from up vector to terrain normal
                objectNode.Rotation = Quaternion.FromRotationTo(Vector3.UnitY, terrain.GetNormal(position));
                objectNode.SetScale(3.0f);
                StaticModel sm = objectNode.CreateComponent<StaticModel>();
                sm.Model = (cache.GetModel(Assets.Models.Mushroom));
                sm.SetMaterial(cache.GetMaterial(Assets.Materials.Mushroom));
                sm.CastShadows = true;

                var body = objectNode.CreateComponent<RigidBody>();
                body.CollisionLayer = CollisionLayer.Static;
                var shape = objectNode.CreateComponent<CollisionShape>();
                shape.SetSphere (1.2f, Vector3.Zero, Quaternion.Identity);
            }
        }

        void SpawnPickups (ResourceCache cache, Terrain terrain)
        {
            const uint count = 1000;
            for (uint i = 0; i < count; ++i)
            {
                Node objectNode = scene.CreateChild("Pickup");
                Vector3 position = new Vector3(NextRandom(2000.0f) - 1000.0f, 0.0f, NextRandom(2000.0f) - 1000.0f);
                position.Y = terrain.GetHeight(position) + 1.8f;
                objectNode.Position = (position);
                // Create a rotation quaternion from up vector to terrain normal
                objectNode.Rotation = Quaternion.FromRotationTo(Vector3.UnitY, terrain.GetNormal(position));
                objectNode.SetScale(3.0f);

                StaticModel sm = objectNode.CreateComponent<Sphere>();
                sm.CastShadows = false;

                var body = objectNode.CreateComponent<RigidBody>();
                body.CollisionLayer = CollisionLayer.Pickup;
                body.Trigger = true;
                var shape = objectNode.CreateComponent<CollisionShape>();
                shape.SetBox (Vector3.One, Vector3.Zero, Quaternion.Identity);
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
            var layout = ResourceCache.GetXmlFile(Assets.UI.ScreenJoystick);
            if (!string.IsNullOrEmpty(JoystickLayoutPatch))
            {
                var patchXmlFile = new XmlFile();
                patchXmlFile.FromString(JoystickLayoutPatch);
                layout.Patch(patchXmlFile);
            }
            var screenJoystickIndex = Input.AddScreenJoystick(layout, ResourceCache.GetXmlFile(Assets.UI.DefaultStyle));
            Input.SetScreenJoystickVisible(screenJoystickIndex, true);
        }

        UrhoConsole console;
        DebugHud debugHud;

        void CreateConsoleAndDebugHud()
        {
            var cache = ResourceCache;

            var xml = cache.GetXmlFile(Assets.UI.DefaultStyle);
            console = Engine.CreateConsole();
            console.DefaultStyle = xml;
            console.Background.Opacity = 0.8f;

            debugHud = Engine.CreateDebugHud();
            debugHud.DefaultStyle = xml;
        }

        Text countLabel;

        void CreateUI()
        {
            countLabel = new Text(Context);
            countLabel.Value = "";
            countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            countLabel.VerticalAlignment = VerticalAlignment.Top;
            countLabel.SetColor(new Color(r: 0f, g: 1f, b: 1f));
            countLabel.SetFont(font: ResourceCache.GetFont("Fonts/Font.ttf"), size: 30);
            UI.Root.AddChild(countLabel);
        }
   }
}
