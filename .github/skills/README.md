# Workspace Skills

This folder contains project-scoped Copilot skills for recurring workflows in this repository.
They complement, but do not replace, the always-on rules in these files:

- `.github/copilot-instructions.md`
- `AGENTS.md`
- `.github/instructions/design-language.instructions.md`
- `.github/instructions/tokens.instructions.md`

Use skills for repeatable, domain-specific implementation workflows. Keep the source-of-truth rules in the docs above and in the architecture/workflow guides; update the skills when those rules change.

## Available Skills

- `comfyui-node-probing` - Probe live ComfyUI `object_info` before wiring node inputs, outputs, or options.
- `workflow-template-authoring` - Add or modify fluent workflow templates, fragments, assets, scopes, and tests.
- `workflow-ui-bridge` - Add or update Generate-page fragment forms and bridge them to `GenerationParameters`.
- `blazor-design-language` - Apply the repo's tab/page/dialog/layout conventions and token-based CSS rules.
- `persistence-migration-playbook` - Add JSON-backed entities, repositories, and hand-authored EF Core migrations.
- `evented-feature-wiring` - Publish and subscribe through `IEventService` instead of ad hoc component coupling.
- `scheduler-feature-workflow` - Implement scheduler jobs, directives, draft persistence, sequencing, and tests.
- `llm-prompt-wildcard-authoring` - Extend wildcard, prompt-template, and send-to flows.
- `resource-library-integration` - Work on CivitAI, Danbooru, resource cache/filter, import, and media browsing flows.
- `targeted-validation` - Choose the narrowest useful validation path in a repo with known unrelated noise.

## Maintenance Notes

- Keep skill descriptions keyword-rich so Copilot can discover them automatically.
- Prefer repo-specific procedures over generic advice.
- Point to the real owning files and docs instead of duplicating large guides.
- For full workflow conversion work that requires a plan and approval gate, keep using `.github/prompts/workflow-conversion.prompt.md`.