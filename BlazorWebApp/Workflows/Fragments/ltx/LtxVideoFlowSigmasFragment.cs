using BlazorWebApp.Workflows.Builders;
using BlazorWebApp.Workflows.Models;
using GenerationParameters = BlazorWebApp.Models.GenerationParameters;

namespace BlazorWebApp.Workflows.Fragments.Ltx;

public class LtxVideoFlowSigmasFragment : IFragmentBuilder
{
    public FragmentMetadata Metadata => new()
    {
        Id = "ltx_videoflow_sigmas",
        Type = FragmentType.Sampler,
        Title = "LTX VideoFlow Sigmas",
        IsHidden = true
    };

    public void Build(
        ComfyWorkflowBuilder builder,
        GenerationParameters parameters,
        NodeRegistry registry,
        string scope = "",
        string scopeTitle = "")
    {
        Build(builder, registry, new Parameters(), scope, scopeTitle);
    }

    public void Build(
        ComfyWorkflowBuilder builder,
        NodeRegistry registry,
        Parameters p,
        string scope = "",
        string scopeTitle = "")
    {
        var prefix = $"{scope}{p.PassId}_videoflow";
        var baseSchedulerId = $"{prefix}_base_scheduler";
        var splitId = $"{prefix}_split_start";
        var catmullId = $"{prefix}_catmull_rom";
        var multId = $"{prefix}_mult";
        var toFloatId = $"{prefix}_to_float";
        var lengthId = $"{prefix}_length";
        var emptyStringId = $"{prefix}_empty_string";
        var loopStartId = $"{prefix}_loop_start";
        var indexId = $"{prefix}_index";
        var calculatorId = $"{prefix}_calculator";
        var appendStringId = $"{prefix}_append_string";
        var loopEndId = $"{prefix}_loop_end";
        var shapedLengthId = $"{prefix}_shaped_length";
        var substringEndId = $"{prefix}_substring_end";
        var substringId = $"{prefix}_substring";
        var stringToFloatId = $"{prefix}_string_to_float";
        var floatToSigmasId = $"{prefix}_float_to_sigmas";
        var unpadId = $"{prefix}_unpad";
        var rescaleId = $"{prefix}_rescale";
        var padId = $"{prefix}_pad";
        var shapedUnpadId = $"{prefix}_shaped_unpad";
        var shapedRescaleId = $"{prefix}_shaped_rescale";
        var shapedPadId = $"{prefix}_shaped_pad";

        builder.AddNode(baseSchedulerId, node => node
            .Type("BasicScheduler")
            .Title($"{scopeTitle}{p.Title} Base Scheduler")
            .Input("scheduler", p.BaseScheduler)
            .Input("steps", p.BaseSchedulerSteps)
            .Input("denoise", 1.0)
            .InputRef("model", registry.GetRef($"{scope}model_output")));

        var schedulerOutputId = baseSchedulerId;
        var schedulerOutputIndex = 0;

        if (p.Start < 1.0)
        {
            builder.AddNode(splitId, node => node
                .Type("Sigmas Split Value")
                .Title($"{scopeTitle}{p.Title} Split Start")
                .InputFromNode("sigmas", baseSchedulerId, 0)
                .Input("split_value", p.Start)
                .Input("bias_split_up", false));

            schedulerOutputId = splitId;
            schedulerOutputIndex = 1;
        }

        builder.AddNode(catmullId, node => node
            .Type("Sigmas CatmullRom")
            .Title($"{scopeTitle}{p.Title} CatmullRom")
            .Input("tension", p.Tension)
            .Input("points", Math.Max(5, p.Steps + 1))
            .Input("boundary_condition", "repeat")
            .InputFromNode("sigmas", schedulerOutputId, schedulerOutputIndex));

        builder.AddNode(multId, node => node
            .Type("Sigmas Mult")
            .Title($"{scopeTitle}{p.Title} Sigma Mult")
            .Input("multiplier", p.Multiplier)
            .InputFromNode("sigmas", catmullId, 0));

        var adjustedSigmasId = multId;

        if (p.Terminal > 0)
        {
            builder.AddNode(unpadId, node => node
                .Type("Sigmas Unpad")
                .Title($"{scopeTitle}{p.Title} Unpad")
                .InputFromNode("sigmas", multId, 0));

            builder.AddNode(rescaleId, node => node
                .Type("Sigmas Rescale")
                .Title($"{scopeTitle}{p.Title} Rescale")
                .Input("start", p.Start)
                .Input("end", p.Terminal)
                .InputFromNode("sigmas", unpadId, 0));

            builder.AddNode(padId, node => node
                .Type("Sigmas Pad")
                .Title($"{scopeTitle}{p.Title} Pad")
                .Input("value", 0.0)
                .InputFromNode("sigmas", rescaleId, 0));

            adjustedSigmasId = padId;
        }

        if (Math.Abs(p.Flatness - 1.0) < 0.000001)
        {
            registry.Register($"{scope}{p.OutputName}", adjustedSigmasId, 0);
            return;
        }

        builder.AddNode(toFloatId, node => node
            .Type("SigmasToFloat")
            .Title($"{scopeTitle}{p.Title} Sigmas To Float")
            .InputFromNode("sigmas", adjustedSigmasId, 0));

        builder.AddNode(lengthId, node => node
            .Type("easy lengthAnything")
            .Title($"{scopeTitle}{p.Title} Float Count")
            .InputFromNode("any", toFloatId, 0));

        builder.AddNode(emptyStringId, node => node
            .Type("PrimitiveString")
            .Title($"{scopeTitle}{p.Title} Empty Sigma String")
            .Input("value", ""));

        builder.AddNode(loopStartId, node => node
            .Type("easy forLoopStart")
            .Title($"{scopeTitle}{p.Title} Loop Start")
            .InputFromNode("total", lengthId, 0)
            .InputFromNode("initial_value1", toFloatId, 0)
            .InputFromNode("initial_value2", emptyStringId, 0));

        builder.AddNode(indexId, node => node
            .Type("easy indexAnything")
            .Title($"{scopeTitle}{p.Title} Index Sigma")
            .InputFromNode("index", loopStartId, 1)
            .InputFromNode("any", loopStartId, 2));

        // Stage 2 clamps the midpoint to (0, start) using small epsilons so the curve
        // formula never collapses when the user picks midpoint == 0 or midpoint >= start.
        // Stage 1 passes the raw user midpoint through (graph parity: 29-node variant).
        var clampedMidpoint = p.Variant == LtxVideoFlowVariant.Stage2
            ? Math.Clamp(p.Midpoint, 0.0001, Math.Max(0.0001, p.Start - 0.0001))
            : p.Midpoint;

        builder.AddNode(calculatorId, node => node
            .Type("SimpleCalculatorKJ")
            .Title($"{scopeTitle}{p.Title} Curve Shape")
            .Input("expression", "c + ((b>c)-(b<c)) * pow(abs(b-c), a) * (((b>c)*(1-c) + (b<c)*c) ** (1-a))")
            .Input("variables.a", p.Flatness)
            .InputFromNode("variables.b", indexId, 0)
            .Input("variables.c", clampedMidpoint));

        builder.AddNode(appendStringId, node => node
            .Type("SomethingToString")
            .Title($"{scopeTitle}{p.Title} Append Sigma")
            .InputFromNode("prefix", loopStartId, 3)
            .Input("suffix", ",")
            .InputFromNode("input", calculatorId, 0));

        builder.AddNode(loopEndId, node => node
            .Type("easy forLoopEnd")
            .Title($"{scopeTitle}{p.Title} Loop End")
            .InputFromNode("flow", loopStartId, 0)
            .InputFromNode("initial_value1", loopStartId, 2)
            .InputFromNode("initial_value2", appendStringId, 0));

        builder.AddNode(shapedLengthId, node => node
            .Type("easy lengthAnything")
            .Title($"{scopeTitle}{p.Title} String Length")
            .InputFromNode("any", loopEndId, 1));

        builder.AddNode(substringEndId, node => node
            .Type("ComfyMathExpression")
            .Title($"{scopeTitle}{p.Title} Trim End")
            .Input("expression", "a - 1")
            .InputFromNode("values.a", shapedLengthId, 0));

        builder.AddNode(substringId, node => node
            .Type("StringSubstring")
            .Title($"{scopeTitle}{p.Title} Trimmed String")
            .InputFromNode("string", loopEndId, 1)
            .Input("start", 0)
            .InputFromNode("end", substringEndId, 1));

        builder.AddNode(stringToFloatId, node => node
            .Type("StringToFloatList")
            .Title($"{scopeTitle}{p.Title} String To Float")
            .InputFromNode("string", substringId, 0));

        builder.AddNode(floatToSigmasId, node => node
            .Type("FloatToSigmas")
            .Title($"{scopeTitle}{p.Title} Float To Sigmas")
            .InputFromNode("float_list", stringToFloatId, 0));

        var outputNodeId = floatToSigmasId;

        if (p.Variant == LtxVideoFlowVariant.Stage2)
        {
            // 42-node "Sigma adjustments needed?" path: resample the shaped curve and rescale
            // to the (start, terminal) range. When the user terminal is 0 the graph uses the
            // second-to-last shaped sigma as a "shifted terminal" so the tail still tapers.
            var stage2CatmullCoarseId = $"{prefix}_stage2_catmull_coarse";
            var stage2MultCoarseId = $"{prefix}_stage2_mult_coarse";
            var stage2SplitId = $"{prefix}_stage2_split_low";
            var stage2CatmullFineId = $"{prefix}_stage2_catmull_fine";
            var stage2MultFineId = $"{prefix}_stage2_mult_fine";
            var stage2UnpadId = $"{prefix}_stage2_unpad";
            var stage2RescaleId = $"{prefix}_stage2_rescale";
            var stage2PadId = $"{prefix}_stage2_pad";
            var stage2ShiftedTerminalId = $"{prefix}_stage2_shifted_terminal";

            builder.AddNode(stage2CatmullCoarseId, node => node
                .Type("Sigmas CatmullRom")
                .Title($"{scopeTitle}{p.Title} Resample Coarse")
                .Input("tension", p.Tension)
                .Input("points", 1000)
                .Input("boundary_condition", "repeat")
                .InputFromNode("sigmas", floatToSigmasId, 0));

            builder.AddNode(stage2MultCoarseId, node => node
                .Type("Sigmas Mult")
                .Title($"{scopeTitle}{p.Title} Resample Mult")
                .Input("multiplier", p.Multiplier)
                .InputFromNode("sigmas", stage2CatmullCoarseId, 0));

            builder.AddNode(stage2SplitId, node => node
                .Type("Sigmas Split Value")
                .Title($"{scopeTitle}{p.Title} Resample Split")
                .InputFromNode("sigmas", stage2MultCoarseId, 0)
                .Input("split_value", p.Start)
                .Input("bias_split_up", true));

            // Graph wires `points` from "Step +1" math expression (Steps + 1), not a static value.
            // Mirrors the Stage 1 catmull formula so the final sigma count matches the user's step count.
            builder.AddNode(stage2CatmullFineId, node => node
                .Type("Sigmas CatmullRom")
                .Title($"{scopeTitle}{p.Title} Resample Fine")
                .Input("tension", p.Tension)
                .Input("points", Math.Max(5, p.Steps + 1))
                .Input("boundary_condition", "repeat")
                .InputFromNode("sigmas", stage2SplitId, 1));

            builder.AddNode(stage2MultFineId, node => node
                .Type("Sigmas Mult")
                .Title($"{scopeTitle}{p.Title} Resample Mult Fine")
                .Input("multiplier", p.Multiplier)
                .InputFromNode("sigmas", stage2CatmullFineId, 0));

            builder.AddNode(stage2UnpadId, node => node
                .Type("Sigmas Unpad")
                .Title($"{scopeTitle}{p.Title} Resample Unpad")
                .InputFromNode("sigmas", stage2MultFineId, 0));

            // Build rescale; end = user terminal when > 0, else the second-to-last shaped
            // sigma (the "Shifted Terminal" trick from the graph) to keep the tail finite.
            if (p.Terminal > 0)
            {
                builder.AddNode(stage2RescaleId, node => node
                    .Type("Sigmas Rescale")
                    .Title($"{scopeTitle}{p.Title} Resample Rescale")
                    .Input("start", p.Start)
                    .Input("end", p.Terminal)
                    .InputFromNode("sigmas", stage2UnpadId, 0));
            }
            else
            {
                builder.AddNode(stage2ShiftedTerminalId, node => node
                    .Type("easy indexAnything")
                    .Title($"{scopeTitle}{p.Title} Shifted Terminal")
                    .Input("index", -2)
                    .InputFromNode("any", stringToFloatId, 0));

                builder.AddNode(stage2RescaleId, node => node
                    .Type("Sigmas Rescale")
                    .Title($"{scopeTitle}{p.Title} Resample Rescale")
                    .Input("start", p.Start)
                    .InputFromNode("end", stage2ShiftedTerminalId, 0)
                    .InputFromNode("sigmas", stage2UnpadId, 0));
            }

            builder.AddNode(stage2PadId, node => node
                .Type("Sigmas Pad")
                .Title($"{scopeTitle}{p.Title} Resample Pad")
                .Input("value", 0.0)
                .InputFromNode("sigmas", stage2RescaleId, 0));

            outputNodeId = stage2PadId;
        }
        else if (p.Terminal > 0)
        {
            builder.AddNode(shapedUnpadId, node => node
                .Type("Sigmas Unpad")
                .Title($"{scopeTitle}{p.Title} Shaped Unpad")
                .InputFromNode("sigmas", floatToSigmasId, 0));

            builder.AddNode(shapedRescaleId, node => node
                .Type("Sigmas Rescale")
                .Title($"{scopeTitle}{p.Title} Shaped Rescale")
                .Input("start", p.Start)
                .Input("end", p.Terminal)
                .InputFromNode("sigmas", shapedUnpadId, 0));

            builder.AddNode(shapedPadId, node => node
                .Type("Sigmas Pad")
                .Title($"{scopeTitle}{p.Title} Shaped Pad")
                .Input("value", 0.0)
                .InputFromNode("sigmas", shapedRescaleId, 0));

            outputNodeId = shapedPadId;
        }

        registry.Register($"{scope}{p.OutputName}", outputNodeId, 0);
    }

    public enum LtxVideoFlowVariant
    {
        Stage1,
        Stage2
    }

    public class Parameters
    {
        public string PassId { get; set; } = "stage1";
        public string OutputName { get; set; } = "stage1_sigmas_output";
        public string Title { get; set; } = "VideoFlow";
        public int Steps { get; set; } = 8;
        public string BaseScheduler { get; set; } = "bong_tangent";
        public int BaseSchedulerSteps { get; set; } = 100;
        public double Tension { get; set; } = 0.5;
        public double Multiplier { get; set; } = 2.0;
        public double Start { get; set; } = 1.0;
        public double Terminal { get; set; } = 0.25;
        public double Midpoint { get; set; } = 0.54;
        public double Flatness { get; set; } = 1.0;
        public LtxVideoFlowVariant Variant { get; set; } = LtxVideoFlowVariant.Stage1;
    }
}