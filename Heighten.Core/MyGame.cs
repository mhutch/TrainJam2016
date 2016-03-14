//
// Copyright (c) 2008-2015 the Urho3D project.
// Copyright (c) 2015 Xamarin Inc
// Copyright (c) 2016 Mikayla Hutchinson
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
//

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Urho;
using Urho.Audio;
using Urho.Gui;
using Urho.Physics;
using Urho.Resources;
using Urho.Shapes;

namespace Heighten
{
    public class MyGame : Application
    {
        public const string GameID = "Heighten";
        public const string GameName = "Heighten";
        public const string GameCopyright = "Copyright © 2016 Mikayla Hutchinson";

        const float TouchSensitivity = 2;

        Vehicle vehicle;
        Scene scene;
        Terrain terrain;

        ListBasedUpdateSynchronizationContext syncContext;

        public Node CameraNode { get; private set; }

        public MyGame() : base(new ApplicationOptions("Data") { WindowedMode = false })
        {
            syncContext = new ListBasedUpdateSynchronizationContext(
                new List<Action>()
            );
            System.Threading.SynchronizationContext.SetSynchronizationContext(syncContext);
        }

        protected override void Start()
        {
            Graphics.WindowTitle = GameName;

            InitTouchInput();

            Input.SubscribeToKeyDown(HandleKeyDown);

            CreateScene();

            SubscribeToEvents();

            CreateConsoleAndDebugHud();

            CreateUI();

            Restart();
        }

        bool started = false;

        void HandleKeyDown(KeyDownEventArgs e)
        {
            switch (e.Key)
            {
                case Key.Esc:
                    if (!started)
                        Engine.Exit();
                    else
                        Restart();
                    return;
                case Key.F:
                    Graphics.ToggleFullscreen();
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

        void Restart()
        {
            started = false;

            if (vehicle != null)
            {
                vehicle.Destroy();
                foreach (var c in scene.Children)
                {
                    var name = c.Name;
                    if (name == "StackingBlock" || name == "Pickup")
                        c.Remove();
                }
            }

            CreateVehicle();
            SpawnPickups();
            RunMessages("Heighten!");
        }

        const float cameraDistance = 15.0f;
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

                syncContext.PumpActions();
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

                    if (!started && (forward || back || left || right))
                    {
                        started = true;
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

        Vector3 worldSize;

        void CreateScene()
        {
            var cache = ResourceCache;

            scene = new Scene();

            var terrainPatchSize = 16;
            var terrainSpacing = new Vector3(0.3f, 0.1f, 0.3f);
            var heightMap = cache.GetImage(Assets.Textures.HeightMap);

            worldSize = new Vector3 (heightMap.Width * terrainSpacing.X, 255 * terrainSpacing.Y, heightMap.Width * terrainSpacing.Z);
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
            zone.AmbientColor = new Color(0.30f, 0.30f, 0.30f);
            zone.FogColor = new Color(0.5f, 0.5f, 0.7f);
            zone.FogStart = 300.0f;
            zone.FogEnd = 500.0f;
            zone.SetBoundingBox(new BoundingBox(-worldSize / 2f, worldSize / 2f));

            // Create a directional light with cascaded shadow mapping
            Node lightNode = scene.CreateChild("DirectionalLight");
            lightNode.SetDirection(new Vector3(0.3f, -0.5f, 0.425f));
            Light light = lightNode.CreateComponent<Light>();
            light.LightType = LightType.Directional;
            light.Color = Color.White;
            light.CastShadows = true;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            light.ShadowCascade = new CascadeParameters(10.0f, 50.0f, 200.0f, 0.0f, 0.8f);
            light.SpecularIntensity = 0.5f;

            lightNode = scene.CreateChild("DirectionalLight");
            lightNode.SetDirection(new Vector3(-0.3f, -0.5f, -0.425f));
            light = lightNode.CreateComponent<Light>();
            light.LightType = LightType.Directional;
            light.Color = Color.Gray;
            light.CastShadows = false;
            light.ShadowBias = new BiasParameters(0.00025f, 0.5f);
            light.SpecularIntensity = 0.5f;

            // Create heightmap terrain with collision
            Node terrainNode = scene.CreateChild("Terrain");
            terrainNode.Position = (Vector3.Zero);
            terrain = terrainNode.CreateComponent<Terrain>();
            terrain.PatchSize = terrainPatchSize;
            terrain.Spacing = terrainSpacing; // Spacing between vertices and vertical resolution of the height map
            terrain.Smoothing = true;
            terrain.SetHeightMap(heightMap);
            terrain.Material = cache.GetMaterial(Assets.Materials.Terrain2);
            // The terrain consists of large triangles, which fits well for occlusion rendering, as a hill can occlude all
            // terrain patches and other objects behind it
            terrain.Occluder = true;

            RigidBody body = terrainNode.CreateComponent<RigidBody>();
            body.CollisionLayer = CollisionLayer.Terrain;
            CollisionShape shape = terrainNode.CreateComponent<CollisionShape>();
            shape.SetTerrain(0);

            //SpawnObstacles(cache, terrain);

            physicsWorld.SubscribeToPhysicsCollision(HandlePhysicsCollision);
            physicsWorld.SubscribeToPhysicsCollisionStart(HandlePhysicsCollisionStart);
        }

        void HandlePhysicsCollision(PhysicsCollisionEventArgs args)
        {
            var layerA = args.BodyA.CollisionLayer;
            var layerB = args.BodyB.CollisionLayer;
            var layers = layerA | layerB;

            if (layers == (CollisionLayer.Pickup | CollisionLayer.Vehicle))
            {
                var node = (layerA == CollisionLayer.Pickup) ? args.NodeA : args.NodeB;
                HandlePickup(node);
                return;
            }

            if (layers == (CollisionLayer.Block | CollisionLayer.Terrain))
            {
                var node = (layerA == CollisionLayer.Block) ? args.NodeA : args.NodeB;
                BlockLost(node);
                return;
            }
        }

        void HandlePhysicsCollisionStart(PhysicsCollisionStartEventArgs args)
        {
            var layerA = args.BodyA.CollisionLayer;
            var layerB = args.BodyB.CollisionLayer;
            var layers = layerA | layerB;

            //if block contacts block or vehicle
            if (layers == (CollisionLayer.Block | CollisionLayer.Vehicle) || layers == CollisionLayer.Block)
            {
                //play click sound
                var source = vehicle.Node.CreateComponent<SoundSource>();
                var sound = Assets.GetNextClickSound();
                source.Play(sound);
                source.Gain = 0.2f;
                source.AutoRemove = true;
                return;
            }
        }

        async void HandlePickup(Node node)
        {
            node.GetComponent<RigidBody>().Enabled = false;

            var source = node.CreateComponent<SoundSource>();
            var sound = ResourceCache.GetSound(Assets.Sounds.Collect);
            source.Play(sound);
            source.Gain = 0.5f;

            SpawnStackingBlock();

            float duration = 0.5f, scale = 1.5f;
            await node.RunActionsAsync(
                new Urho.Actions.Parallel(
                    new Urho.Actions.ScaleBy(duration, scale),
                    new Urho.Actions.FadeOut(duration)
                ),
                new Urho.Actions.DelayTime (sound.Length)
            );
            node.Remove();

            SpawnPickupAndFadeIn();
        }

        async void BlockLost(Node node)
        {
            //fall through terrain
            node.GetComponent<RigidBody>().CollisionMask ^= CollisionLayer.Terrain;

            UpdateCountLabel(-1);

            var source = node.CreateComponent<SoundSource>();
            var sound = Assets.GetNextLossSound();
            source.Play(sound);
            source.Gain = 0.5f;

            float duration = 0.5f, scale = 1.5f;
            await node.RunActionsAsync(
                  new Urho.Actions.ScaleBy(duration, scale),
                  new Urho.Actions.DelayTime(sound.Length)
            );
            node.Remove();
        }

        const float minBlockSpawnDelay = 0.1f;
        //hopefully precision won't be an issue
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
            box.SetMaterial(Assets.GetNextBlockMaterial ());

            var body = node.CreateComponent<RigidBody>();
            body.CollisionLayer = CollisionLayer.Block;
            body.Mass = 4f;
            body.Friction = 1f;
            body.RollingFriction = 1f;
            body.Restitution = 0.01f;
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

            node.Scale /= 100f;
            node.RunActionsAsync(new Urho.Actions.ScaleTo(0.2f, 3f, 1f, 3f));
        }

        int liveBlocks;

        void UpdateCountLabel(int delta)
        {
            liveBlocks += delta;

            if (liveBlocks == 0)
            {
                blockLabel.Value = "";
                RunMessages("Awwwww...", "Try again!", "");
                return;
            }

            blockLabel.Value = liveBlocks.ToString ().PadLeft (3);

            if (delta > 0)
            {
                if (liveBlocks == 1)
                {
                    RunMessages("Good start!", "");
                    return;
                }

                if (liveBlocks == 5)
                {
                    RunMessages("Halfway there!", "");
                    return;
                }

                if (liveBlocks == 9)
                {
                    RunMessages("So close!", "");
                    return;
                }

                if (liveBlocks == 10)
                {
                    RunMessages("Yaaaaaay!", "Congratulations!");
                    return;
                }
            }

            if (delta < 0)
            {
                if (liveBlocks == 1)
                {
                    RunMessages("Almost back where you started...", "");
                    return;
                }

                if (liveBlocks == 3)
                {
                    RunMessages("Uh oh...", "");
                    return;
                }

                if (liveBlocks == 7)
                {
                    RunMessages("Ooops...", "");
                    return;
                }
            }
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

        void SpawnPickups ()
        {
            const uint count = 50;
            for (uint i = 0; i < count; ++i)
            {
                SpawnPickup();
            }
        }

        Node SpawnPickup()
        {
            //HACK: brute forcing valid locations, bail out after 10 failures
            //could mean we gradually lose nodes over time
            for (int attempts = 0; attempts < 10; attempts++)
            {
                var position = new Vector3(NextRandom(worldSize.X) - worldSize.X / 2f, 0.0f, NextRandom(worldSize.Z) - worldSize.Z / 2f);

                var terrainHeight = terrain.GetHeight(position);
                position.Y = terrainHeight + 2.2f;

                //make sure it's not above the wall
                if (position.Y > worldSize.Y)
                    continue;

                Node node = scene.CreateChild("Pickup");
                node.Position = (position);

                //don't place them when the angle is too steep
                //TODO check not too close to a wall too
                var terrainNormal = terrain.GetNormal(position);
                if (terrainNormal.Y < 0.8)
                    continue;

                node.SetScale(2.0f);

                Sphere sm = node.CreateComponent<Sphere>();
                sm.CastShadows = false;
                sm.Color = new Color(1f, 1f, 0.2f, 0.7f);

                var body = node.CreateComponent<RigidBody>();
                body.CollisionLayer = CollisionLayer.Pickup;
                body.Trigger = true;
                var shape = node.CreateComponent<CollisionShape>();
                shape.SetBox(Vector3.One, Vector3.Zero, Quaternion.Identity);
                return node;
            }
            return null;
        }

        async void SpawnPickupAndFadeIn()
        {
            var node = SpawnPickup();
            if (node == null)
                return;
            node.GetComponent<RigidBody>().Enabled = false;
            await node.RunActionsAsync(new Urho.Actions.FadeIn (0.2f));
            node.GetComponent<RigidBody>().Enabled = true;
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

        Text countLabel, blockLabel;

        void CreateUI()
        {
            countLabel = new Text(Context);
            countLabel.Value = "";
            countLabel.HorizontalAlignment = HorizontalAlignment.Center;
            countLabel.VerticalAlignment = VerticalAlignment.Top;
            countLabel.SetColor(Color.Red);
            countLabel.SetFont(font: ResourceCache.GetFont("Fonts/Font.ttf"), size: 30);
            UI.Root.AddChild(countLabel);

            blockLabel = new Text(Context);
            blockLabel.Value = "";
            blockLabel.HorizontalAlignment = HorizontalAlignment.Left;
            blockLabel.VerticalAlignment = VerticalAlignment.Bottom;
            blockLabel.SetColor(Color.Red);
            blockLabel.SetFont(font: ResourceCache.GetFont("Fonts/Font.ttf"), size: 30);
            UI.Root.AddChild(blockLabel);
        }
   }
}
