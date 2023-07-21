# Cocaine .NET
Are [steroids](https://github.com/subspecs/Steroids) not doing it for you anymore?<br>
Need that 'kick' in the mornings to get you through the day?

### Look no further because we have the solution for you!

### Meet <ins>**Cocaine .NET**</ins>!
A multi-platform C# library that can be used to accelerate <ins>large</ins> workloads/big data/<ins>anything really</ins> with the power of a GPU with ease. <br>
And you know what's the best part of it? You can even let grandma do a line.


### That's right!
The library uses ONLY the default OpenGL 4.3 or Open GL ES 3.1 features with no extensions.<br>
Meaning this thing works on toasters/solitaire/potato/trashcan machines that were made from 2010* and up.
<br>
<sub>*Some SOCs and GPUs like ATI/AMD/Intel didn't bother to update their API levels, tho a very small portion of them.</sub><br>

For what **methods** are available, their **documentation** and **signatures** are available at the [wiki]().(SOON)<br>
There are also comments on everything*.<br>

### <ins>How does one use it?</ins>
Simply head to the [releases](https://github.com/subspecs/CocaineNET/releases) section and download the latest one for your platform.<br>
Link the **CocaineNET.dll** to your .NET project and don't forget to add **Cocaine.dll** next to your compiled executable. (And also add **glfw3.dll** if on windows.)<br>

**That's it!**<br>
**Example usage**:
```C#
using CocaineAPI;

namespace ConsoleApp1
{
    internal class Program
    {
        public static void OnError(Cocaine.ErrorTypes Type, string Message)
        {
            System.Console.WriteLine(Type.ToString() + ": " + Message);
        }

        static void Main(string[] args)
        {
            Cocaine.OnError += OnError; //Subscribe event for error events.

            Cocaine.GPUDevice[] GPUs = Cocaine.GetGPUDevices(); //Get the currently detected GPUs on the running system. (Set 'Update' to true for a refreshed list.)

            if (!Cocaine.InitializeGPU(GPUs[0])) //Initialized the first detected GPU.
            {
                throw new System.Exception("Failed to initialize the GPU!");
            }

            Cocaine.SetActiveGPUOnThread(GPUs[0]); //Bind/Set the first detected GPU as the active gpu on the calling thread. Main thread in this case.

            int[] InputArrayA = new int[] { 210, 210, 210, 210 }; //Create a data buffer with some values.
            int[] InputArrayB = new int[] { 210, 210, 210, 210 }; //Create a data buffer with some values.
            int[] Output = new int[4]; //Create an buffer where we're going to store our work when its done.

            Cocaine.CreateBuffer(Cocaine.GPUBufferTypes.Int, "InputA", InputArrayA, 0, 4, Cocaine.GPUBufferOptimizations.GPUStatic); //Create a buffer on GPU VRAM memory and write InputArrayA to it.
            Cocaine.CreateBuffer(Cocaine.GPUBufferTypes.Int, "InputB", InputArrayB, 0, 4, Cocaine.GPUBufferOptimizations.GPUStatic); //Create a buffer on GPU VRAM memory and write InputArrayB to it.
            Cocaine.CreateBuffer<int>(Cocaine.GPUBufferTypes.Int, "Output", null, 0, 4, Cocaine.GPUBufferOptimizations.Read); //Create a buffer on GPU VRAM memory and don't write anything so that the GPU creates an empty/zero filled buffer.

            //Compile our GLSL(version 430) compatible code.
            if (!Cocaine.CompileComputeProgram("void main() { Output[CSJobIndex] = InputA[CSJobIndex] + InputB[CSJobIndex]; }", out Cocaine.GPUProgram Program)) //Compile Shader code. (Note: CSJobIndex is a global variable for the current Process ID.)
            {
                throw new System.Exception("Failed to compile program! Refer to the error output.");
            }

            if (!Cocaine.RunComputeProgram(Program, 4)) //Run our program on our active GPU.
            {
                throw new System.Exception("Failed to run program! Refer to the error output.");
            }

            Cocaine.ReadFromBuffer("Output", 0, Output, 0, 4); //Now when the program is done, we read back the 'Output' buffer on the GPU where our finished work is stored.

            int n = 0;
            while (n < Output.Length)
            {
                System.Console.WriteLine(InputArrayA[n] + " + " + InputArrayB[n] + " = " + Output[n]); //Lets print out the results in 'Output'.
                n++;
            }

            System.Console.ReadKey(true); //Hold execution so we can preview our results.
        }
    }
}
```

### <ins>How does one compile it?</ins>
Just download/clone the latest source or download a release build source, open **CocaineNET.sln**, choose a compile target platform and hit compile.<br>
If you're looking for on how to compile the core **Cocaine.dll** then take a look the [Cocaine Repo](https://github.com/subspecs/Cocaine).

### **Want to [help me](https://www.patreon.com/subspecs) pay off my therapist? Might make updates more frequent!**


