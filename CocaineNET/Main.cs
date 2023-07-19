namespace CocaineAPI
{
    namespace Legacy
    {
        public enum GPUBufferTypes { NoReadWrite, Read, FastRead, ReadWrite }
        public unsafe struct GPUBuffer { public uint Buffer; public int BufferID; };
        public unsafe delegate void GLFWError(sbyte* ErrorID, sbyte* FunctionName, sbyte* ErrorDesc);
        public unsafe delegate void ProgramShaderError(sbyte* ErrorDesc);
        public unsafe struct GPUDevice { public void* GPUContext; public void* GPUMonitor; public sbyte* DisplayName; public sbyte* MonitorName; public fixed int GPUDeviceLimits[3]; };
        
        public static class Methods
        {
            private const string LibraryPath = @"Cocaine";

            //OS Methods =============================================================================
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe long GetCurrentTimestamp();
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe long GetTimestampSeconds(long Timestamp);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe long GetTimestampMilliseconds(long Timestamp);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe long GetTimestampMicroseconds(long Timestamp);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe long GetTimestampNanoseconds(long Timestamp);

            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            internal static extern unsafe void RegisterOnGLFWErrorMethod(GLFWError Method);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            internal static extern unsafe void RegisterOnProgramRunErrorMethod(ProgramShaderError Method);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            internal static extern unsafe void RegisterOnShaderCompileErrorMethod(ProgramShaderError Method);


            //GPU Methods ============================================================================
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void Initialize();
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void ReleaseResources();
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void RefreshGPUList();

            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe int CreateGPUContext(GPUDevice* Device);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void DisposeGPUContext(GPUDevice* Device);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe int GetRawGPUDevices(GPUDevice** OutDevices);

            //API Methods ============================================================================
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe GPUBuffer AllocateGPUBuffer(GPUBufferTypes BufferType, int BufferID, void* ByteBuffer, long ByteCount);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void DeallocateGPUBuffer(GPUBuffer* GPUBuffer);

            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void ReadFromGPUBuffer(GPUBuffer* GPUBuffer, void* Buffer, long GPUBufferOffset, long Count);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void WriteToGPUBuffer(GPUBuffer* GPUBuffer, void* Buffer, long GPUBufferOffset, long Count);

            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe int CompileProgram(sbyte* ShaderCode, uint* OutProgram);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe int LoadComputeProgram(byte* Buffer, int Count, uint* Program);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe int SaveComputeProgram(uint Program, byte* Buffer);

            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe void SetActiveGPUContext(GPUDevice* Device);
            [System.Runtime.InteropServices.DllImport(LibraryPath)]
            public static extern unsafe int RunComputeProgram(uint Program, int* GPUDeviceLimits, long ProcessCount, int PreciseCycleCount);
        }
    }

    public static class Cocaine
    {
        public enum ErrorTypes
        {
            Internal, ShaderCompile, ProgramRun
        }
        public enum GPUBufferTypes
        {
            Float, Double, Int, Uint, Bool, Vec, DVec, BVec, IVec, UVec, Mat2, DMat2, Mat3, DMat3, Mat4, DMat4, Custom
        }
        public enum GPUBufferOptimizations
        {
            /// <summary>
            /// Optimizes the buffer for maximum compute speed, reading/writing from GPU to CPU and viceversa might be slower.
            /// </summary>
            GPUStatic,
            /// <summary>
            /// Optimizes the buffer for reading, reading the buffer from the GPU to the CPU will be at above average speeds, but writing from CPU to GPU might not.
            /// </summary>
            Read,
            /// <summary>
            /// Does no real optimization and allows for proper read/write at decent speeds from GPU to CPU and viceversa.
            /// </summary>
            ReadWrite
        }
        public class GPUProgram { internal uint CompiledProgram; }
        public class GPUBuffer { public string BufferType; public string BufferName; public int BufferLength; internal int BufferID; internal Legacy.GPUBuffer GBuffer; };
        public class GPUDevice { public string GPUName, MonitorName; internal unsafe int* GPUDeviceLimits; internal unsafe Legacy.GPUDevice* Device; };

        /// <summary>
        /// Subscribe for all errors relating to the internal API.
        /// </summary>
        public static event System.Action<ErrorTypes, string> OnError;

        private static bool IsDisposed, NeedGPUUpdate;
        [System.ThreadStatic]
        private static GPUDevice ThreadActiveGPU;
        [System.ThreadStatic]
        private static int ThreadBufferCounter;
        [System.ThreadStatic]
        private static System.Collections.Generic.Dictionary<string, GPUBuffer> ThreadActiveBuffers;

        //Constructor.
        static Cocaine()
        {
            Legacy.Methods.Initialize();
            unsafe
            {
                Legacy.Methods.RegisterOnGLFWErrorMethod(OnGLFWError);
                Legacy.Methods.RegisterOnShaderCompileErrorMethod(OnShaderCompileError);
                Legacy.Methods.RegisterOnProgramRunErrorMethod(OnProgramRunError);
            }
        }

        //Internal Methods.
        private static sbyte[] FuckingStringToSByte(string Input)
        {
            sbyte[] Buffer = new sbyte[Input.Length + 1];
            int n = 0; while (n < Input.Length) { Buffer[n] = (sbyte)Input[n]; n++; }
            Buffer[n] = 0;
            return Buffer;
        }
        private static unsafe void OnGLFWError(sbyte* ErrorID, sbyte* FunctionName, sbyte* ErrorDesc)
        {
            if (OnError != null)
            {
                OnError.Invoke(ErrorTypes.Internal, new string(ErrorID) + " @ '" + new string(FunctionName) + "': " + new string(ErrorDesc));
            }
        }
        private static unsafe void OnShaderCompileError(sbyte* ErrorDesc)
        {
            if (OnError != null)
            {
                OnError.Invoke(ErrorTypes.ShaderCompile, new string(ErrorDesc));
            }
        }
        private static unsafe void OnProgramRunError(sbyte* ErrorDesc)
        {
            if (OnError != null)
            {
                OnError.Invoke(ErrorTypes.ProgramRun, new string(ErrorDesc));
            }
        }

        //Public Methods
        /// <summary>
        /// Disposes the library and releases its resources.
        /// </summary>
        /// <exception cref="System.ObjectDisposedException"></exception>
        public static void Dispose()
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            else
            {
                Legacy.Methods.ReleaseResources(); IsDisposed = true;
            }
        }
        /// <summary>
        /// Gets the currently detected GPU devices. Must be called only from main thread.
        /// </summary>
        /// <param name="Update">If true, re-detects connected GPUs on system. <b>WARNING</b>: ONLY set this to true when ALL GPUs are disposed/aren't initialized!</param>
        /// <returns>Return detected GPU devices.</returns>
        public static GPUDevice[] GetGPUDevices(bool Update = false)
        {
            unsafe
            {
                if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
                if (Update && NeedGPUUpdate) { Legacy.Methods.RefreshGPUList(); }

                Legacy.GPUDevice* OutDevices = null;
                int DeviceCount = Legacy.Methods.GetRawGPUDevices(&OutDevices);

                GPUDevice[] GPUDevices = new GPUDevice[DeviceCount];
                int n = 0; while (n < DeviceCount)
                {
                    GPUDevices[n] = new GPUDevice();
                    GPUDevices[n].GPUName = new string(OutDevices[n].DisplayName);
                    GPUDevices[n].MonitorName = new string(OutDevices[n].MonitorName);
                    GPUDevices[n].GPUDeviceLimits = OutDevices[n].GPUDeviceLimits;
                    GPUDevices[n].Device = &OutDevices[n];
                    n++;
                }

                NeedGPUUpdate = true;
                return GPUDevices;
            }
        }

        /// <summary>
        /// Checks if GPU is initialized.
        /// </summary>
        /// <param name="GPU">The GPU to check.</param>
        /// <returns>Returns true if GPU is initialized, otherwise returns false.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool IsGPUInitialized(GPUDevice GPU)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (GPU == null) { throw new System.Exception("The GPU cannot be null."); }
            unsafe
            {
                return GPU.Device[0].GPUContext != null;
            }
        }
        /// <summary>
        /// Initializes the GPU. Must be called only from main thread.
        /// </summary>
        /// <param name="GPU">The GPU to initialize.</param>
        /// <returns>Returns true if its initialized, otherwise returns false.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool InitializeGPU(GPUDevice GPU)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (GPU == null) { throw new System.Exception("The GPU cannot be null."); }
            unsafe
            {
                if (GPU.Device[0].GPUContext != null) { throw new System.Exception("The GPU is already initialized."); }
                if (Legacy.Methods.CreateGPUContext(GPU.Device) == 0) { return false; }
                return true;
            }
        }
        /// <summary>
        /// Dispose an initialized GPU. Must be called only from main thread.
        /// </summary>
        /// <param name="GPU"></param>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static void DisposeGPU(GPUDevice GPU)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (GPU == null) { throw new System.Exception("The GPU cannot be null."); }
            unsafe
            {
                if (GPU.Device[0].GPUContext == null) { throw new System.Exception("The GPU is isn't initialized."); }
                Legacy.Methods.DisposeGPUContext(GPU.Device);
            }
        }

        /// <summary>
        /// Checks if the GPU is currently active on the calling thread.
        /// </summary>
        /// <param name="GPU">The GPU to check.</param>
        /// <returns>Returns true if it is active, otherwise returns false.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool IsGPUActiveOnThread(GPUDevice GPU)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (GPU == null) { throw new System.Exception("The GPU cannot be null."); }
            return ThreadActiveGPU == GPU;
        }
        /// <summary>
        /// Sets the active GPU for the calling thread from which this method was invoked from.
        /// </summary>
        /// <param name="GPU">The GPU to be made active on the calling thread.</param>
        /// <exception cref="System.Exception"></exception>
        public static void SetActiveGPUOnThread(GPUDevice GPU)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (GPU == null) { throw new System.Exception("The GPU cannot be null."); }

            unsafe
            {
                Legacy.Methods.SetActiveGPUContext(GPU.Device);
                ThreadActiveGPU = GPU;
            }
        }

        /// <summary>
        /// Checks if the buffer exists on the active GPU on the calling thread.
        /// </summary>
        /// <param name="BufferName">The Buffer Name to check.</param>
        /// <returns>Returns true if it exists, otherwise returns false.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool IsBufferCreated(string BufferName)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (BufferName == null) { throw new System.Exception("BufferName cannot be null"); }
            return ThreadActiveBuffers != null && ThreadActiveBuffers.ContainsKey(BufferName);
        }
        /// <summary>
        /// Creates an buffer on the GPU memory and uploads blittable data to it. Buffer is created on the active GPU on the calling thread.
        /// </summary>
        /// <typeparam name="T">Has to be a blittable struct.</typeparam>
        /// <param name="BufferType">The type of the buffer.</param>
        /// <param name="BufferName">The variable name of the buffer.</param>
        /// <param name="Buffer">Data to be buffered to the GPU buffer.</param>
        /// <param name="Offset">Offset from where to read the Data from.</param>
        /// <param name="Count">How many elements to read from Data.</param>
        /// <param name="Optimization">Specify what optimization this buffer is going to use.</param>
        /// <param name="CustomBufferType">If you set <b>BufferType</b> to Custom, then you can specify a custom GLSL buffer type, however the custom type has to be declared BEFORE the buffer decleration in the GLSL shader. Check documentation for more info.</param>
        /// <exception cref="System.Exception"></exception>
        public static void CreateBuffer<T>(GPUBufferTypes BufferType, string BufferName, T[] Buffer, int Offset, int Count, GPUBufferOptimizations Optimization, string CustomBufferType = null) where T : unmanaged
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (BufferName == null) { throw new System.Exception("Buffer name cannot be null."); }
            if (ThreadActiveBuffers != null && ThreadActiveBuffers.ContainsKey(BufferName)) { throw new System.Exception("Buffer of that name already exists."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }
            switch (BufferType)
            {
                case GPUBufferTypes.Custom: { if (Count >= 1) { CustomBufferType += "[]"; } } break;

                case GPUBufferTypes.Bool: { if (Count == 1) { CustomBufferType = "bool"; } else { CustomBufferType = "bool[]"; } } break;
                case GPUBufferTypes.BVec: { if (Count == 1) { CustomBufferType = "bvec"; } else { CustomBufferType = "bvec[]"; } } break;
                case GPUBufferTypes.DMat2: { if (Count == 1) { CustomBufferType = "dmat2"; } else { CustomBufferType = "dmat2[]"; } } break;
                case GPUBufferTypes.DMat3: { if (Count == 1) { CustomBufferType = "dmat3"; } else { CustomBufferType = "dmat3[]"; } } break;
                case GPUBufferTypes.DMat4: { if (Count == 1) { CustomBufferType = "dmat4"; } else { CustomBufferType = "dmat4[]"; } } break;
                case GPUBufferTypes.Double: { if (Count == 1) { CustomBufferType = "double"; } else { CustomBufferType = "double[]"; } } break;
                case GPUBufferTypes.DVec: { if (Count == 1) { CustomBufferType = "dvec"; } else { CustomBufferType = "dvec[]"; } } break;
                case GPUBufferTypes.Float: { if (Count == 1) { CustomBufferType = "float"; } else { CustomBufferType = "float[]"; } } break;
                case GPUBufferTypes.Int: { if (Count == 1) { CustomBufferType = "int"; } else { CustomBufferType = "int[]"; } } break;
                case GPUBufferTypes.IVec: { if (Count == 1) { CustomBufferType = "ivec"; } else { CustomBufferType = "ivec[]"; } } break;
                case GPUBufferTypes.Mat2: { if (Count == 1) { CustomBufferType = "mat2"; } else { CustomBufferType = "mat2[]"; } } break;
                case GPUBufferTypes.Mat3: { if (Count == 1) { CustomBufferType = "mat3"; } else { CustomBufferType = "mat3[]"; } } break;
                case GPUBufferTypes.Mat4: { if (Count == 1) { CustomBufferType = "mat4"; } else { CustomBufferType = "mat4[]"; } } break;
                case GPUBufferTypes.Uint: { if (Count == 1) { CustomBufferType = "uint"; } else { CustomBufferType = "uint[]"; } } break;
                case GPUBufferTypes.UVec: { if (Count == 1) { CustomBufferType = "uvec"; } else { CustomBufferType = "uvec[]"; } } break;
                case GPUBufferTypes.Vec: { if (Count == 1) { CustomBufferType = "vec"; } else { CustomBufferType = "vec[]"; } } break;
            }

            Legacy.GPUBufferTypes BType;
            switch (Optimization)
            {
                case GPUBufferOptimizations.Read: { BType = Legacy.GPUBufferTypes.Read; } break;
                case GPUBufferOptimizations.ReadWrite: { BType = Legacy.GPUBufferTypes.ReadWrite; } break;
                default: { BType = Legacy.GPUBufferTypes.NoReadWrite; } break;
            }

            unsafe
            {
                if (ThreadActiveBuffers == null) { ThreadActiveBuffers = new System.Collections.Generic.Dictionary<string, GPUBuffer>(2); }
                GPUBuffer NewBuffer = new GPUBuffer();
                NewBuffer.BufferName = BufferName;
                NewBuffer.BufferType = CustomBufferType;
                NewBuffer.BufferID = ThreadBufferCounter;
                NewBuffer.BufferLength = sizeof(T) * Count;

                if (Buffer == null)
                {
                    NewBuffer.GBuffer = Legacy.Methods.AllocateGPUBuffer(BType, NewBuffer.BufferID, null, NewBuffer.BufferLength);
                }
                else { fixed (void* InBuffer = &Buffer[Offset]) { NewBuffer.GBuffer = Legacy.Methods.AllocateGPUBuffer(BType, NewBuffer.BufferID, InBuffer, NewBuffer.BufferLength); } }
                ThreadActiveBuffers.Add(BufferName, NewBuffer);
                ThreadBufferCounter++;
            }
        }
        /// <summary>
        /// Removes and previously created buffer. Buffer is removed on the active GPU on the calling thread.
        /// </summary>
        /// <param name="BufferName">Name of the buffer to be removed.</param>
        /// <exception cref="System.Exception"></exception>
        public static void RemoveBuffer(string BufferName)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (BufferName == null) { throw new System.Exception("BufferName cannot be null"); }
            if (ThreadActiveBuffers != null && !ThreadActiveBuffers.ContainsKey(BufferName)) { throw new System.Exception("Buffer of that name doesn't exist."); }

            unsafe
            {
                fixed (Legacy.GPUBuffer* InBuffer = &ThreadActiveBuffers[BufferName].GBuffer)
                {
                    Legacy.Methods.DeallocateGPUBuffer(InBuffer);
                }
                ThreadActiveBuffers.Remove(BufferName);
            }
        }

        /// <summary>
        /// Reads data from a GPU VRAM Buffer on the active GPU on the calling thread.
        /// </summary>
        /// <typeparam name="T">Has to be a blittable struct.</typeparam>
        /// <param name="BufferName">The variable name of the buffer.</param>
        /// <param name="BufferOffset">The GPU buffer offset to read from.</param>
        /// <param name="Buffer">Where to read in data from GPU buffer.</param>
        /// <param name="Offset">Offset from where to start writing to Buffer.</param>
        /// <param name="Count">How many elements to read from the GPU buffer.</param>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static void ReadFromBuffer<T>(string BufferName, long BufferOffset, T[] Buffer, int Offset, int Count) where T : unmanaged
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (BufferName == null) { throw new System.Exception("Buffer name cannot be null."); }
            if (ThreadActiveBuffers == null || !ThreadActiveBuffers.ContainsKey(BufferName)) { throw new System.Exception("Buffer of that name doesn't exists."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }

            unsafe
            {
                fixed (Legacy.GPUBuffer* GBuffer = &ThreadActiveBuffers[BufferName].GBuffer)
                {
                    fixed (void* InBuffer = &Buffer[Offset])
                    {
                        Legacy.Methods.ReadFromGPUBuffer(GBuffer, InBuffer, BufferOffset, sizeof(T) * Count);
                    }
                }
            }
        }
        /// <summary>
        /// Writes data to a GPU VRAM Buffer on the active GPU on the calling thread.
        /// </summary>
        /// <typeparam name="T">Has to be a blittable struct.</typeparam>
        /// <param name="BufferName">The variable name of the buffer.</param>
        /// <param name="BufferOffset">The GPU buffer offset to write to.</param>
        /// <param name="Buffer">From where we write data to the GPU buffer.</param>
        /// <param name="Offset">Offset from where to start writing from buffer to GPU buffer.</param>
        /// <param name="Count">How many elements to write to the GPU buffer.</param>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static void WriteToBuffer<T>(string BufferName, long BufferOffset, T[] Buffer, int Offset, int Count) where T : unmanaged
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (BufferName == null) { throw new System.Exception("Buffer name cannot be null."); }
            if (ThreadActiveBuffers == null || !ThreadActiveBuffers.ContainsKey(BufferName)) { throw new System.Exception("Buffer of that name doesn't exists."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }

            unsafe
            {
                fixed (Legacy.GPUBuffer* GBuffer = &ThreadActiveBuffers[BufferName].GBuffer)
                {
                    fixed (void* InBuffer = &Buffer[Offset])
                    {
                        Legacy.Methods.WriteToGPUBuffer(GBuffer, InBuffer, BufferOffset, sizeof(T) * Count);
                    }
                }
            }
        }

        /// <summary>
        /// Compiles an GLSL compute shader program on the active GPU on the calling thread. 
        /// <br>Note: The compute program is not binded to the GPU/calling thread, this method only req. a GPU to be active on the calling thread.</br>
        /// </summary>
        /// <param name="SourceCode">Shader Code.</param>
        /// <param name="Program">Output of the compiled program.</param>
        /// <param name="CustomTypeDefinitions">If you created GPU buffers with the type <b>Custom</b> then you'll need to declare structs on the GPU shader to cast them into. (<b>Note</b>: This will appended in front of the <b>SourceCode</b>.)
        /// <br>Ex: <b>struct MyType { int Item1; Double Item2; };</b></br>
        /// </param>
        /// <returns>Returns true if successfull, otherwise returns false.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool CompileComputeProgram(string SourceCode, out GPUProgram Program, string CustomTypeDefinitions = null)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (SourceCode == null) { throw new System.ObjectDisposedException("ShaderCode cannot be null."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }

            const string JobIndex = "unsigned int CSJobIndex = (gl_GlobalInvocationID.x * (gl_NumWorkGroups.y * gl_NumWorkGroups.z)) + (gl_GlobalInvocationID.y * gl_NumWorkGroups.z) + gl_GlobalInvocationID.z;\n";

            unsafe
            {
                System.Text.StringBuilder SB = new System.Text.StringBuilder(5 + (ThreadActiveBuffers.Count * 8));
                SB.Append("#version 430\n");
                SB.Append("layout(local_size_x = 1, local_size_y = 1, local_size_z = 1) in;\n");
                if (CustomTypeDefinitions != null) { SB.Append(CustomTypeDefinitions); }
                var Enum = ThreadActiveBuffers.GetEnumerator();
                while (Enum.MoveNext())
                {
                    SB.Append("layout(std430, binding = ");
                    SB.Append(Enum.Current.Value.BufferID);
                    SB.Append(") buffer CSBuffer");
                    SB.Append(Enum.Current.Value.BufferID);
                    SB.Append(" { ");
                    if (Enum.Current.Value.BufferType.Contains("[]"))
                    {
                        SB.Append(Enum.Current.Value.BufferType.Replace("[]", "") + " ");
                        SB.Append(Enum.Current.Value.BufferName + "[]");
                    }
                    else { SB.Append(Enum.Current.Value.BufferType + " "); SB.Append(Enum.Current.Value.BufferName); }
                    SB.Append("; };\n");
                }
                Enum.Dispose();
                SB.Append(JobIndex);
                SB.Append(SourceCode);

                var Buffer = FuckingStringToSByte(SB.ToString());
                uint SProgram = 0; Program = null;

                fixed (sbyte* InBuffer = Buffer)
                {
                    if (Legacy.Methods.CompileProgram(InBuffer, &SProgram) == 0)
                    {
                        return false;
                    }

                }
                Program = new GPUProgram() { CompiledProgram = SProgram };
                return true;
            }
        }
        /// <summary>
        /// Loads a compute program from memory on the active GPU on the calling thread. 
        /// <br>Note: The compute program is not binded to the GPU/calling thread, this method only req. a GPU to be active on the calling thread.</br> 
        /// </summary>
        /// <param name="Buffer"></param>
        /// <param name="Offset"></param>
        /// <param name="Count"></param>
        /// <param name="Program"></param>
        /// <returns>Returns true if successfull, otherwise returns false.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool LoadComputeProgram(byte[] Buffer, int Offset, int Count, out GPUProgram Program)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (Buffer == null) { throw new System.ObjectDisposedException("Buffer cannot be null."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }

            unsafe
            {
                uint SProgram = 0; Program = null;
                fixed (byte* InBuffer = &Buffer[Offset])
                {
                    if (Legacy.Methods.LoadComputeProgram(InBuffer, Count, &SProgram) == 0)
                    {
                        return false;
                    }
                    Program = new GPUProgram() { CompiledProgram = SProgram };
                    return true;
                }
            }
        }
        /// <summary>
        /// Saves a compute program to memory on the active GPU on the calling thread. 
        /// <br>Note: The compute program is not binded to the GPU/calling thread, this method only req. a GPU to be active on the calling thread.</br> 
        /// </summary>
        /// <param name="GPUProgram">The program to be saved.</param>
        /// <param name="Buffer">The buffer where to save out compute program.</param>
        /// <param name="Offset">Offset in the buffer where to start saving.</param>
        /// <returns>Returns the amount of bytes written to the buffer.</returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static int SaveComputeProgram(GPUProgram Program, byte[] Buffer, int Offset)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (Buffer == null) { throw new System.ObjectDisposedException("Buffer cannot be null."); }
            if (Program == null) { throw new System.ObjectDisposedException("Program cannot be null."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }

            unsafe
            {
                fixed (byte* InBuffer = &Buffer[Offset])
                {
                    return Legacy.Methods.SaveComputeProgram(Program.CompiledProgram, InBuffer);
                }
            }
        }

        /// <summary>
        /// Executes the compute program on the active GPU on the calling thread. 
        /// </summary>
        /// <param name="Program">Program to be executed.</param>
        /// <param name="ProcessCount">The amount of times to run it. Think of it as <b>for</b> cycle iterations.</param>
        /// <returns></returns>
        /// <exception cref="System.ObjectDisposedException"></exception>
        /// <exception cref="System.Exception"></exception>
        public static bool RunComputeProgram(GPUProgram Program, long ProcessCount)
        {
            if (IsDisposed) { throw new System.ObjectDisposedException("API"); }
            if (Program == null) { throw new System.ObjectDisposedException("Program cannot be null."); }
            if (ProcessCount <= 0) { throw new System.ObjectDisposedException("ProcessCount has to be > 0."); }
            if (ThreadActiveGPU == null) { throw new System.Exception("This method can only be called from a thread that has an active GPU on it."); }

            unsafe
            {
                if (Legacy.Methods.RunComputeProgram(Program.CompiledProgram, ThreadActiveGPU.GPUDeviceLimits, ProcessCount, 0) == 0)
                {
                    return false;
                }
                return true;
            }
        }
    }
}
