﻿using BspLib.Bsp;
using BulletSharp;
using BulletSharp.Math;
using DemoFramework;
using GoldsrcPhysics.ExportAPIs;
using GoldsrcPhysics.Goldsrc;
using GoldsrcPhysics.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq.Expressions;
using System.Reflection;
using System.Runtime.InteropServices;

namespace GoldsrcPhysics.ExportAPIs
{
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void InitDelegate(IntPtr p,IntPtr addr);
    
    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void DoWork();

    [UnmanagedFunctionPointer(CallingConvention.StdCall)]
    public delegate void UpdateDelegate(float delta);

    [StructLayout(LayoutKind.Sequential)]
    public struct PhysicsAPI
    {
        //[MarshalAs( UnmanagedType.FunctionPtr)]
        //public InitDelegate InitSystem;

        //[MarshalAs(UnmanagedType.FunctionPtr)]
        //public UpdateDelegate Update;

        //[MarshalAs(UnmanagedType.FunctionPtr)]
        //public DoWork Test;
        public IntPtr a;
        public IntPtr b;
        public IntPtr c;
    }
    public static class DelegateCreator
    {
        public static readonly Func<Type[], Type> MakeNewCustomDelegate =
            (Func<Type[], Type>)Delegate.CreateDelegate
            (
                typeof(Func<Type[], Type>),
                typeof(Expression).Assembly.GetType("System.Linq.Expressions.Compiler.DelegateHelpers")
                .GetMethod("MakeNewCustomDelegate", BindingFlags.NonPublic | BindingFlags.Static)
            );
        public static Type NewDelegateType(Type ret, params Type[] parameters)
        {
            Type[] args = new Type[parameters.Length + 1];
            parameters.CopyTo(args, 0);
            args[args.Length - 1] = ret;
            return MakeNewCustomDelegate(args);
        }
    }
    /// <summary>
    /// Provide the interface to goldsrc that can access to
    /// </summary>
    public unsafe class PhysicsMain
    {
        #region static properties

        private static RagdollManager RagdollManager { get; set; }

        private static LocalPlayerBodyPicker LocalBodyPicker { get; set; }

        private static List<RigidBody> SceneStaticObjects { get; } = new List<RigidBody>();

        private static bool IsPaused;

        #endregion

        #region RegisterPhysicsAPI
        //purpose of this region: called by native code and write the API function pointer for native 

        [MarshalAs(UnmanagedType.FunctionPtr)]
        private static InitDelegate Init;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        private static UpdateDelegate Up;
        [MarshalAs(UnmanagedType.FunctionPtr)]
        private static DoWork Do;

        // Holds the api instance avoid being garbage collected
        private static PhysicsAPI API;

        /// <summary>
        /// Register APIs
        /// </summary>
        /// <param name="physicsAPI">[in]</param>
        [DllImport("client.dll",CallingConvention = CallingConvention.Cdecl)]
        private static extern void RegisterAPI(in PhysicsAPI physicsAPI);

        public static int InitPhysicsInterface(string msg)
        {
            Debug.LogLine("Welcome to goldsrc physics. host msg:\"{0}\"", msg);
            var moddir=PhyConfiguration.GetValue("ModDir");
            Environment.SetEnvironmentVariable("Path", Path.Combine(moddir, "cl_dlls"));
            Init = new InitDelegate(InitSystem);
            Up = new UpdateDelegate(Update);
            Do = new DoWork(TestAPI.Test);
            API = new PhysicsAPI()
            {
                //InitSystem = InitSystem,
                //Update = Update,
                //Test = TestAPI.Test
                a = (IntPtr)GetMethodPointer("InitSystem"),
                b = Marshal.GetFunctionPointerForDelegate(Up),
                c = Marshal.GetFunctionPointerForDelegate(Do),
            };
            RegisterAPI(in API);
            return 0;
        }

        /// <summary>
        /// Fill the struct with API function pointer.
        /// But in older version of CLR host, it can't call this function directly. 
        /// You should use InitPhysicsInterface instead, and implement RegisterAPI in your native host to save physicsAPI for future use.
        /// </summary>
        /// <param name="physicsAPI">[out]</param>
        public static void GetPhysicsInterface(ref PhysicsAPI physicsAPI)
        {
            //physicsAPI.InitSystem = InitSystem;
            //physicsAPI.Update = Update;
            //physicsAPI.Test = TestAPI.Test;
        }

        #endregion

        #region Get API pointer

        public static int GetInterface(string pFuncPtr)
        {
            void** p = (void**)Convert.ToUInt64(pFuncPtr);
            p[0] = GetMethodPointer("GetMethodPointer");
            return (int)p[0];
        }

        //avoid gc collect this object
        static List<object> KeepReference = new List<object>();
        public unsafe static void* GetMethodPointer(string name)
        {
            System.Reflection.MethodInfo methodInfo = typeof(PhysicsMain).GetMethod(name);

            // also mark this delegate with [UnmanagedFunctionPointer(CallingConvention.StdCall)] attribute
            // default marshal calling convension is stdcall
            Type delegateType = ConstructDelegateTypeWithMethodInfo(methodInfo);

            var delegateInstance = Delegate.CreateDelegate(delegateType, methodInfo);

            KeepReference.Add(delegateType);
            KeepReference.Add(delegateInstance);
            return (void*)Marshal.GetFunctionPointerForDelegate(delegateInstance);
        }
        public static int GetFunctionPointer(string pointerAndName)
        {
            var args = pointerAndName.Trim().Split('|');
            if (args.Length > 2)
                throw new ArgumentException(nameof(pointerAndName) + ":" + pointerAndName);
            void** p = (void**)Convert.ToUInt64(args[0],16);
            p[0] = GetMethodPointer(args[1]);
            return (int)p[0];
        }
        public static Type ConstructDelegateTypeWithMethodInfo(MethodInfo info)
        {
            List<Type> argTypes = new List<Type>();
            foreach (var i in info.GetParameters())
                argTypes.Add(i.ParameterType);
            argTypes.Add(info.ReturnType);
            return DelegateCreator.MakeNewCustomDelegate(argTypes.ToArray());
        }
        #endregion
        #region Test
        public static void Test()
        {
            TestAPI.Test();
        }
        #endregion
        #region PhysicsSystem

        public static unsafe void InitSystem(IntPtr pStudioRenderer,IntPtr lastFieldAddress)
        {
            //register goldsrc global variables
            //拿到金源引擎的API，使物理引擎可以访问缓存的模型信息、地图信息等
            BWorld.CreateInstance();
            StudioRenderer.Init(pStudioRenderer);
            StudioRenderer.Drawer = BWorld.Instance.DebugDrawer;
            RagdollManager = new RagdollManager();
            LocalBodyPicker = new LocalPlayerBodyPicker();

            //Validation
            if ((IntPtr)(&StudioRenderer.NativePointer->m_plighttransform)!=lastFieldAddress)
                throw new Exception("studio model renderer is invalid.");
        }
        /// <summary>
        /// Load map
        /// </summary>
        public static void ChangeLevel(string mapName)
        {
            for (int i = 0; i < SceneStaticObjects.Count; i++)
            {
                BWorld.Instance.RemoveRigidBody(SceneStaticObjects[i]);
                SceneStaticObjects[i].Dispose();
            }
            SceneStaticObjects.Clear();
            LoadScene(mapName);
        }
        /// <summary>
        /// 地图不变，内容重置，清理在游戏中动态创建的各种CollisionObjects
        /// cs的每一局结束可以调用
        /// </summary>
        public static void LevelReset()
        {

        }

        /// <summary>
        /// Physics world update
        /// </summary>
        /// <param name="delta"></param>
        public static void Update(float delta)
        {
            if (IsPaused)
                return;
            //handling input
            //player's collider pos, bodypicker's pos
            LocalBodyPicker.Update();

            //physics simulating
            if (delta < 0)
                Time.SubStepCount += BWorld.Instance.StepSimulation(Time.DeltaTime);
            else
                Time.SubStepCount += BWorld.Instance.StepSimulation(delta);

            //drawing
            {//not covered draw
                //BWorld.Instance.DebugDrawWorld();
                //just put lines in buffer, then you should draw it on ViewDrawing
            }
            {//normal draw
                (BWorld.Instance.DebugDrawer as PhysicsDebugDraw).DrawDebugWorld(BWorld.Instance);
            }
        }

        public static void Pause()
        {
            IsPaused = true;
        }

        public static void Resume()
        {
            IsPaused = false;
        }

        /// <summary>
        /// Close physics system and release physics resources.
        /// </summary>
        public static void ShotDown()
        {

        }
        #endregion

        #region RagdollAPI
        public static void CreateRagdollController(int entityId, string modelName)
        {
            RagdollManager.CreateRagdollController(entityId, modelName);
        }
        public static void StartRagdoll(int entityId)
        {
            RagdollManager.StartRagdoll(entityId);
        }
        public static void StopRagdoll(int entityId)
        {
            RagdollManager.StopRagdoll(entityId);
        }
        public static void SetupBonesPhysically(int entityId)
        {
            RagdollManager.SetupBonesPhysically(entityId);
        }

        public static void ChangeOwner(int oldEntity, int newEntity)
        {
            RagdollManager.ChangeOwner(oldEntity, newEntity);
        }

        public static void SetVelocity(int entityId, Vector3 v)
        {
            RagdollManager.SetVelocity(entityId, v);
        }
        #endregion

        #region BodyPicker
        public static void PickBody()
        {
            LocalBodyPicker.PickBody();
        }

        public static void ReleaseBody()
        {
            LocalBodyPicker.Release();
        }

        #endregion

        #region PrivateMethod
        private static void LoadScene(string levelName)
        {
            var path = PhyConfiguration.GetValue("MapDir");
            var filePath = Path.Combine( path , levelName + ".bsp");
            Debug.LogLine("Load map {0}", filePath);

            LoadBsp(filePath);
        }
        static string[] EntityWithInvisableModel =
{
            "func_buyzone",
            "func_bomb_target"
        };
        /// <summary>
        /// 每个地图都由固体和固体实体和点实体构成
        /// 地图的Model[0]就是地图的静态地形
        /// WorldSpawn实体是每个地图都有的实体
        /// 地图有许多个Model，每个Model里面有许多的子模型，因为每个子模型应用不同的材质。
        /// 出于性能目的，如果只是贴图不同而其他渲染操作相同，可以合并贴图、UV、子模型，这样可以合并为一个draw call
        /// 对于物理引擎，合并模型也能提高性能，而在物理世界只考虑几何模型，所以我们可以合并所有静态的子模型。
        /// 预览版：排除不可见固体实体的模型、排除贴上不参与碰撞贴图的模型（如天空）
        /// 正式版：只加载静态地形，包括func_wall实体的模型，排除贴上不参与碰撞贴图的模型（如天空），其他实体的模型应参与游戏逻辑（如门应该是Kinematic刚体，一般的物件应该是动态刚体）
        /// </summary>
        /// <param name="path"></param>
        private static void LoadBsp(string path)
        {
            List<int> invisableModelIndex = new List<int>();
            BspFile bsp = new BspFile();
            BspFile.LoadAllFromFile(bsp, BspFile.LoadFlags.Visuals | BspFile.LoadFlags.Entities, path);
            foreach (var i in bsp.Entities)
            {
                string classname = "";
                if (!i.TryGetValue("classname", out classname))
                    continue;

                for (int j = 0; j < EntityWithInvisableModel.Length; j++)
                {
                    if (classname == EntityWithInvisableModel[j])
                    {
                        invisableModelIndex.Add(Convert.ToInt32(i["model"].Substring(1)));
                        break;
                    }
                }
            }
            List<BvhTriangleMeshShape> shapes = new List<BvhTriangleMeshShape>();
            for (int i = 0; i < bsp.Models.Count; i++)
            {
                if (invisableModelIndex.Contains(i))
                    continue;

                var bspmodel = bsp.Models[i];
                //var gameObject = new GameObject("" + i);
                //gameObject.transform.SetParent(go_all_models.transform);
                //if (i == 0)//如果是静态地图（）
                //{
                //    gameObject.isStatic = true;
                //    gameObject.layer = settings.Model0Layer;
                //}
                //else
                //{
                //    gameObject.layer = settings.OtherModelLayer;
                //}

                // Count triangles
                int submeshCountWithoutSky = 0;
                System.Collections.Generic.KeyValuePair<string, uint[]> kvp_sky = default(KeyValuePair<string, uint[]>);
                foreach (var kvp in bspmodel.Triangles)
                {
                    if (kvp.Key == "sky")
                    {
                        kvp_sky = kvp;
                    }
                    else
                        submeshCountWithoutSky++;
                }

                // Create Mesh
                //var mesh = new Mesh();
                //mesh.name = string.Format("Model {0}", i);
                // Submesh Count
                //mesh.subMeshCount = submeshCountWithoutSky;
                //mesh_renderer.materials = new Material[model.Triangles.Count];

                // Create vertices and uv
                var vertices = new List<Vector3>();
                //var uv = new List<Vector2>();
                for (int ii = 0; ii < bspmodel.Positions.Length; ii++)
                {
                    var p = bspmodel.Positions[ii];
                    vertices.Add(new Vector3(p.X * GBConstant.G2BScale, p.Y * GBConstant.G2BScale, p.Z * GBConstant.G2BScale));

                    //var t = bspmodel.TextureCoordinates[ii];
                    //uv.Add(new Vector2(t.x, -t.y));
                }

                //mesh.SetUVs(0, uv);

                //var materials = new Material[submeshCountWithoutSky];

                int submesh = 0;
                List<int> triangles = new List<int>();
                foreach (var kvp in bspmodel.Triangles)
                {
                    if (kvp.Key == "sky")
                        continue;

                    var indices = new int[kvp.Value.Length];
                    for (int ii = 0; ii < indices.Length; ii += 3)
                    {
                        //indices[ii + 0] = (int)kvp.Value[ii + 0];
                        //indices[ii + 1] = (int)kvp.Value[ii + 1];
                        //indices[ii + 2] = (int)kvp.Value[ii + 2];
                        triangles.Add((int)kvp.Value[ii + 0]);
                        triangles.Add((int)kvp.Value[ii + 1]);
                        triangles.Add((int)kvp.Value[ii + 2]);
                    }
                    //mesh.SetTriangles(indices, submesh: submesh);

                    submesh++;
                }
                var meshShape = new BvhTriangleMeshShape(new TriangleIndexVertexArray(triangles, vertices), true);
                shapes.Add(meshShape);

            }
            foreach (var shape in shapes)
            {
                SceneStaticObjects.Add( PhysicsHelper.CreateStaticBody(Matrix.Translation(0, 0, 0), shape, BWorld.Instance));
            }
        }
        #endregion



        //#region Obsolete
        //const string ObsoleteMsg = "this class is deprecated, please use PhysicsMain class instead.";
        //public static Simulation WorldSimulation;

        //public static int AddBspWorld(string bsppath)
        //{
        //    //WorldSimulation.LoadBsp(@"E:\sjz\xash3d_fwgs_win32_0.19.2\valve\maps\crossfire.bsp");
        //    var args = bsppath.Split('|');
        //    GoldsrcRagdoll.StudioPositions = new StudioPositions(Convert.ToInt32(args[0]));
        //    GoldsrcRagdoll.StudioQuaternions = new StudioQuaternions(Convert.ToInt32(args[1]));
        //    GoldsrcRagdoll.StudioBoneMatrices = new StudioBoneMatrices(Convert.ToInt32(args[2]));
        //    return 0;
        //}

        //public static int StartSimulation(string noneed)
        //{
        //    WorldSimulation = new Simulation();
        //    WorldSimulation.Run();
        //    return 0;
        //}

        //public static int AddRagdoll(string arg)
        //{
        //    WorldSimulation.AddRagdoll();
        //    return 0;
        //}
        //public static int UpdateRagdoll(string arg)
        //{
        //    WorldSimulation.UpdateRagdoll(0);
        //    return 0;
        //}
        //public static int Update(string noarg)
        //{
        //    throw new NotImplementedException();
        //    //WorldSimulation.FixedUpdate();
        //    return 0;
        //}

        //public static int AddCube(string ptr)
        //{
        //    if (WorldSimulation == null)
        //        return 1;
        //    WorldSimulation.SpawnBox(Convert.ToInt32(ptr));
        //    return 0;
        //}
        //#endregion

    }
}
