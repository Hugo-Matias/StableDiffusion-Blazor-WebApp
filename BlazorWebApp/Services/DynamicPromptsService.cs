using Python.Runtime;

namespace BlazorWebApp.Services
{
    public class DynamicPromptsService
    {
        private readonly IConfiguration _configuration;

        public DynamicPromptsService(IConfiguration configuration)
        {
            _configuration = configuration;
            Environment.SetEnvironmentVariable("PYTHONNET_PYDLL", _configuration["PythonPath"]);
            PythonEngine.Initialize();
            PythonEngine.BeginAllowThreads();
            Test();
        }

        public void Test()
        {
            int dotnetNumber = 3;
            using var _ = Py.GIL();
            using var scope = Py.CreateScope();
            scope.Set("pythonNumber", dotnetNumber);
            scope.Exec("calculation = pythonNumber * 2");
            dynamic calc = scope.Get("calculation");
            Console.WriteLine("Calculation from Python: " + calc);
        }
    }
}
