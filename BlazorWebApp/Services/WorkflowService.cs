using BlazorWebApp.Data.Converters;
using BlazorWebApp.Data.Entities;
using BlazorWebApp.Models;
using Scriban;
using Scriban.Runtime;
using System.Text.Json;
using System.Text.RegularExpressions;
using static BlazorWebApp.Data.Enums;

namespace BlazorWebApp.Services
{
    public class WorkflowService
    {
        private readonly string _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");
        private readonly IOService _io;

        public WorkflowService(IOService io)
        {
            _io = io;
        }

        public List<Workflow> GetWorkflows()
        {
            var workflowFiles = _io.GetFilesRecursive(_workflowPath, ignorePath: "utils", extensionsWhitelist: new() { ".scriban" });
            List<Workflow> workflows = new();

            foreach (var filePath in workflowFiles)
            {
                var jsonText = File.ReadAllText(filePath.FullName);
                var workflow = ParseWorkflowFile(jsonText);
                workflow.Id = Guid.NewGuid();
                workflows.Add(workflow);
            }

            return workflows;
        }

        // Parse the .scriban data as raw text and deserialize "prompt" as a string to be rendered later for the api request
        // Avoids issues with deserialization of Scriban syntax
        private Workflow ParseWorkflowFile(string workflowText)
        {
            // Find the start and end of the "prompt" JSON object value.
            const string promptKey = "\"prompt\":";
            int keyIndex = workflowText.IndexOf(promptKey, StringComparison.InvariantCultureIgnoreCase);
            if (keyIndex == -1)
            {
                throw new ArgumentException("The input JSON does not contain a 'prompt' key.");
            }

            int valueStartIndex = workflowText.IndexOf('{', keyIndex + promptKey.Length);
            if (valueStartIndex == -1)
            {
                throw new ArgumentException("Could not find the opening '{' for the 'prompt' value.");
            }

            int braceCount = 1;
            int valueEndIndex = -1;
            for (int i = valueStartIndex + 1; i < workflowText.Length; i++)
            {
                if (workflowText[i] == '{') braceCount++;
                if (workflowText[i] == '}') braceCount--;
                if (braceCount == 0)
                {
                    valueEndIndex = i;
                    break;
                }
            }

            if (valueEndIndex == -1)
            {
                throw new ArgumentException("Could not find the matching closing '}' for the 'prompt' object.");
            }

            string rawPrompt = workflowText.Substring(valueStartIndex, valueEndIndex - valueStartIndex + 1);

            // Create a valid JSON string by replacing the raw prompt object with a placeholder.
            string placeholder = $"\"_{Guid.NewGuid().ToString()}_\"";
            string validJsonString = string.Concat(
                workflowText.AsSpan(0, valueStartIndex),
                placeholder,
                workflowText.AsSpan(valueEndIndex + 1)
            );

            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters =
                {
                    new CaseInsensitiveEnumConverter<ModeType>(),
                    new CaseInsensitiveEnumConverter<ModelType>(),
                    new CaseInsensitiveEnumConverter<ModelBase>()
                }
            };
            Workflow workflow = JsonSerializer.Deserialize<Workflow>(validJsonString, options);

            if (workflow != null)
            {
                workflow.Prompt = rawPrompt;
            }

            return workflow;
        }

        public JsonDocument Render(string workflow, object parameters)
        {
            var template = Template.Parse(workflow);

            if (template.HasErrors)
                throw new InvalidOperationException($"Template parse errors: {string.Join(", ", template.Messages)}");

            // Use context to access built-in filters like '| json'
            var context = new TemplateContext() { MemberRenamer = member => member.Name, MemberFilter = null };
            context.BuiltinObject.Import("json", new Func<object, string>(o => JsonSerializer.Serialize(o)));


            var scriptObject = new ScriptObject();
            scriptObject.Import(parameters, renamer: member => member.Name);
            context.PushGlobal(scriptObject);

            var rendered = template.Render(context);
            rendered = rendered.Replace("\r", "").Replace("\n", "").Trim();
            rendered = Regex.Replace(rendered, @"\s+", " ");
            return JsonDocument.Parse(rendered);
        }
    }
}
