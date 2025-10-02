using Scriban;
using Scriban.Runtime;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace BlazorWebApp.Services
{
    public class WorkflowService
    {
        private readonly string _workflowPath = Path.Combine(AppContext.BaseDirectory, "Workflows");

        public JsonDocument Render(string templateName, object parameters)
        {
            var filePath = Path.Combine(_workflowPath, templateName);
            if (!File.Exists(filePath))
                throw new FileNotFoundException($"Template {templateName} not found in {_workflowPath}");

            var templateText = File.ReadAllText(filePath);
            var template = Template.Parse(templateText);

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
