// For examples, see:
// https://thegraybook.vvvv.org/reference/extending/writing-nodes.html#examples

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.IO;
using System.Text;
using System.IO.Compression;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace Main;

public static class Utils
{
    /// <summary>Extracts VL nodes from a workflow as {id,title,class_type,inputs}.</summary>
    public static JArray GetVLNodes(JObject workflow)
    {
        var result = new JArray();
        if (workflow is null) return result;

        foreach (var prop in workflow.Properties())
        {
            if (prop.Value is not JObject node) continue;
            var meta = node["_meta"] as JObject;
            var title = meta? ["title"]?.Value<string>();
            if (string.IsNullOrEmpty(title) || !title.StartsWith("VL ", StringComparison.Ordinal)) continue;

            var obj = new JObject
            {
                ["id"] = prop.Name,
                ["title"] = title,
                ["class_type"] = node["class_type"]?.Value<string>() ?? string.Empty,
                ["inputs"] = node["inputs"] is JObject inputs ? (JToken)inputs.DeepClone() : new JObject()
            };
            result.Add(obj);
        }

        return result;
    }

    // Minimal 2-node API
    /// <summary>Exports VL node summaries as a JSON string.</summary>
    public static string ExportVLNodesJson(JObject workflow, bool pretty = true)
    {
        var arr = GetVLNodes(workflow);
        return arr.ToString(pretty ? Formatting.Indented : Formatting.None);
    }

    /// <summary>Exports VL node summaries as JArray and JSON string.</summary>
    public static void ExportVLNodes(JObject workflow, bool pretty, out JArray summaries, out string json)
    {
        summaries = GetVLNodes(workflow);
        json = summaries.ToString(pretty ? Formatting.Indented : Formatting.None);
    }

    // 2) Apply edited summaries JSON back to workflow (in-place)
    //    summariesJson: JSON array of { title: string, inputs: { ... } }
    //    Only existing input keys are updated; connections are never overwritten; no new inputs are added.
    /// <summary>Applies edited summaries JSON back onto the workflow.</summary>
    public static JObject ApplyVLNodesFromJson(JObject workflow, string summariesJson)
    {
        if (workflow is null) return new JObject();
        if (string.IsNullOrWhiteSpace(summariesJson)) return workflow;
        var arr = JArray.Parse(summariesJson);
        // Use strict apply: allowNewInputs=false, overwriteConnections=false
        return ApplyVLNodeSummaries(workflow, arr, allowNewInputs: false, overwriteConnections: false);
    }

    /// <summary>Applies summaries array onto the workflow.</summary>
    public static JObject ApplyVLNodes(JObject workflow, JArray summaries)
    {
        return ApplyVLNodeSummaries(workflow, summaries, allowNewInputs: false, overwriteConnections: false);
    }

    // Edit helper: modify a single param value inside summaries by title and param name
    // - Handles numeric coercion (tries double with invariant culture), otherwise uses string
    // - Does not modify connection arrays
    // - Returns a new JArray (deep copy) and a pretty string of the selected summary
    /// <summary>Edits a parameter value across all summaries with the same title (in-place).</summary>
    public static void ApplyValue(JArray summaries, string title, string param, string newValue,
        out JArray updatedSummaries, out string selectedSummaryInfo)
    {
        // In-place mutation so multiple ApplyValue nodes can act on the same object without chaining
        updatedSummaries = summaries ?? new JArray();
        selectedSummaryInfo = string.Empty;
        ApplyValueToSummariesInPlace(updatedSummaries, title, param, newValue, out selectedSummaryInfo);
    }

    /// <summary>Creates a change spec object {title,param,value}.</summary>
    public static JObject BuildChangeSpec(string title, string param, string newValue)
    {
        return new JObject
        {
            ["title"] = title ?? string.Empty,
            ["param"] = param ?? string.Empty,
            ["value"] = newValue ?? string.Empty
        };
    }

    // Apply multiple value changes without chaining
    // specs: array of { title: string, param: string, value: string }
    /// <summary>Applies multiple change specs to summaries without chaining.</summary>
    public static void ApplyValueSpecs(JArray summaries, JArray specs,
        out JArray updatedSummaries, out string appliedReport)
    {
        updatedSummaries = (summaries?.DeepClone() as JArray) ?? new JArray();
        var applied = new List<string>();
        if (specs is null) { appliedReport = string.Empty; return; }

        foreach (var s in specs.OfType<JObject>())
        {
            var title = s["title"]?.Value<string>() ?? string.Empty;
            var param = s["param"]?.Value<string>() ?? string.Empty;
            var val = s["value"]?.Value<string>() ?? string.Empty;
            ApplyValueToSummariesInPlace(updatedSummaries, title, param, val, out _);
            if (!string.IsNullOrWhiteSpace(title) && !string.IsNullOrWhiteSpace(param))
            {
                applied.Add($"{title} :: {param}");
            }
        }

        appliedReport = string.Join(Environment.NewLine, applied);
    }

    /// <summary>Applies change specs directly to a workflow.</summary>
    public static JObject ApplyValueSpecsToWorkflow(JObject workflow, JArray specs)
    {
        var summaries = GetVLNodes(workflow);
        ApplyValueSpecs(summaries, specs, out var updatedSummaries, out _);
        return ApplyVLNodes(workflow, updatedSummaries);
    }

    /// <summary>Mutates summaries for a single change (internal).</summary>
    private static void ApplyValueToSummariesInPlace(JArray updatedSummaries, string title, string param, string newValue, out string selectedSummaryInfo)
    {
        selectedSummaryInfo = string.Empty;
        if (updatedSummaries is null || updatedSummaries.Count == 0 || string.IsNullOrWhiteSpace(title)) return;

        var targets = new List<JObject>();
        foreach (var s in updatedSummaries.OfType<JObject>())
        {
            var t = s["title"]?.Value<string>();
            if (string.Equals(t, title, StringComparison.Ordinal))
            {
                targets.Add(s);
            }
        }

        if (targets.Count == 0) return;

        // If param is empty, just expose the first matched summary for info and exit
        selectedSummaryInfo = targets[0].ToString(Formatting.Indented);
        if (string.IsNullOrWhiteSpace(param)) return;

        foreach (var target in targets)
        {
            var inputs = target["inputs"] as JObject;
            if (inputs is null)
            {
                inputs = new JObject();
                target["inputs"] = inputs;
            }

            var existing = inputs[param];
            if (existing is null) continue; // strict: do not add new keys
            if (IsConnection(existing)) continue; // do not override connections

            JToken proposed;
            if (existing.Type == JTokenType.Integer || existing.Type == JTokenType.Float)
            {
                if (string.IsNullOrEmpty(newValue)) continue;
                if (double.TryParse(newValue, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d))
                {
                    proposed = existing.Type == JTokenType.Integer ? new JValue((long)Math.Round(d)) : new JValue(d);
                }
                else
                {
                    proposed = new JValue(newValue);
                }
            }
            else if (existing.Type == JTokenType.Boolean)
            {
                if (bool.TryParse(newValue, out var b)) proposed = new JValue(b);
                else continue;
            }
            else if (existing.Type == JTokenType.String)
            {
                proposed = new JValue(NormalizePotentialPath(newValue ?? string.Empty));
            }
            else
            {
                continue; // arrays/objects unsupported here
            }

            inputs[param] = CoerceToExistingType(existing, proposed);
            selectedSummaryInfo = target.ToString(Formatting.Indented);
        }
    }

    // Summaries helpers
    /// <summary>Finds a summary by VL node title.</summary>
    public static JObject? FindVLNodeSummaryByTitle(JArray summaries, string title)
    {
        if (summaries is null || string.IsNullOrWhiteSpace(title)) return null;
        foreach (var s in summaries.OfType<JObject>())
        {
            var t = s["title"]?.Value<string>();
            if (string.Equals(t, title, StringComparison.Ordinal)) return s;
        }
        return null;
    }

    /// <summary>Filters summaries by title substring (case-insensitive).</summary>
    public static JArray FilterVLNodeSummaries(JArray summaries, string contains)
    {
        var result = new JArray();
        if (summaries is null || string.IsNullOrEmpty(contains)) return result;
        foreach (var s in summaries.OfType<JObject>())
        {
            var t = s["title"]?.Value<string>() ?? string.Empty;
            if (t.IndexOf(contains, StringComparison.OrdinalIgnoreCase) >= 0)
            {
                result.Add(s.DeepClone());
            }
        }
        return result;
    }

    /// <summary>Returns the inputs object from a node summary.</summary>
    public static JObject GetInputsFromVLNode(JObject summary)
    {
        if (summary is null) return new JObject();
        return summary["inputs"] as JObject ?? new JObject();
    }

    // Get inputs object for a node by its unique VL title
    /// <summary>Gets the inputs object for a node by VL title.</summary>
    public static JObject GetVLNodeInputs(JObject workflow, string vlTitle)
    {
        if (workflow is null || string.IsNullOrWhiteSpace(vlTitle)) return new JObject();
        var nodeProp = FindNodePropByVLTitle(workflow, vlTitle);
        if (nodeProp is null) return new JObject();
        var node = (JObject)nodeProp.Value;
        return node["inputs"] as JObject ?? new JObject();
    }

    // Apply new inputs (partial) to a node by its unique VL title
    // Apply inputs (in-place) to a node identified by VL title
    /// <summary>Applies input overrides to all nodes that match a VL title.</summary>
    public static JObject ApplyVLNodeInputs(
        JObject workflow,
        string vlTitle,
        JObject newInputs,
        bool allowNewInputs = false,
        bool overwriteConnections = false)
    {
        if (workflow is null || string.IsNullOrWhiteSpace(vlTitle) || newInputs is null) return workflow ?? new JObject();
        var matches = FindNodePropsByVLTitle(workflow, vlTitle).ToList();
        if (matches.Count == 0) return workflow;

        foreach (var nodeProp in matches)
        {
            var node = (JObject)nodeProp.Value;
            var inputs = node["inputs"] as JObject;
            if (inputs is null)
            {
                inputs = new JObject();
                node["inputs"] = inputs;
            }

            foreach (var kv in newInputs.Properties())
            {
                var key = kv.Name;
                var proposed = kv.Value;
                var existing = inputs[key];

                if (existing is null)
                {
                    if (!allowNewInputs) continue;
                    inputs[key] = proposed.DeepClone();
                    continue;
                }

                if (IsConnection(existing) && !overwriteConnections)
                {
                    continue;
                }

                inputs[key] = CoerceToExistingType(existing, proposed);
            }
        }

        return workflow;
    }

    /// <summary>Applies a single node summary to the workflow.</summary>
    public static JObject ApplyVLNodeSummary(
        JObject workflow,
        JObject summary,
        bool allowNewInputs = false,
        bool overwriteConnections = false)
    {
        if (workflow is null || summary is null) return workflow ?? new JObject();
        var title = summary["title"]?.Value<string>();
        var inputs = summary["inputs"] as JObject;
        if (string.IsNullOrWhiteSpace(title) || inputs is null) return workflow;
        return ApplyVLNodeInputs(workflow, title!, inputs, allowNewInputs, overwriteConnections);
    }

    // Apply a batch of overrides: [{ "title": "VL ...", "inputs": { ... } }]
    // Batch apply overrides (in-place)
    // overrides: array of { title: string, inputs: { ... } }
    /// <summary>Batch-applies overrides {title,inputs} to the workflow.</summary>
    public static JObject ApplyBatchVLOutputs(
        JObject workflow,
        JArray overrides,
        bool allowNewInputs = false,
        bool overwriteConnections = false)
    {
        if (workflow is null || overrides is null) return workflow ?? new JObject();

        foreach (var ov in overrides.OfType<JObject>())
        {
            var title = ov["title"]?.Value<string>();
            var inputs = ov["inputs"] as JObject;
            if (string.IsNullOrWhiteSpace(title) || inputs is null) continue;
            ApplyVLNodeInputs(workflow, title!, inputs, allowNewInputs, overwriteConnections);
        }

        return workflow;
    }

    /// <summary>Applies multiple summaries to the workflow.</summary>
    public static JObject ApplyVLNodeSummaries(
        JObject workflow,
        JArray summaries,
        bool allowNewInputs = false,
        bool overwriteConnections = false)
    {
        if (workflow is null || summaries is null) return workflow ?? new JObject();
        foreach (var s in summaries.OfType<JObject>())
        {
            ApplyVLNodeSummary(workflow, s, allowNewInputs, overwriteConnections);
        }
        return workflow;
    }

    // Private helpers
    /// <summary>Finds the workflow property for a node by VL title.</summary>
    private static JProperty? FindNodePropByVLTitle(JObject workflow, string vlTitle)
    {
        if (workflow is null || string.IsNullOrWhiteSpace(vlTitle)) return null;
        foreach (var prop in workflow.Properties())
        {
            if (prop.Value is not JObject node) continue;
            var meta = node["_meta"] as JObject;
            var title = meta? ["title"]?.Value<string>();
            if (string.Equals(title, vlTitle, StringComparison.Ordinal))
            {
                return prop;
            }
        }
        return null;
    }

    /// <summary>Finds all workflow properties for nodes by VL title.</summary>
    private static IEnumerable<JProperty> FindNodePropsByVLTitle(JObject workflow, string vlTitle)
    {
        if (workflow is null || string.IsNullOrWhiteSpace(vlTitle)) yield break;
        foreach (var prop in workflow.Properties())
        {
            if (prop.Value is not JObject node) continue;
            var meta = node["_meta"] as JObject;
            var title = meta? ["title"]?.Value<string>();
            if (string.Equals(title, vlTitle, StringComparison.Ordinal))
            {
                yield return prop;
            }
        }
    }

    /// <summary>Returns true if token represents a connection tuple.</summary>
    private static bool IsConnection(JToken token)
    {
        if (token is JArray arr && arr.Count == 2)
        {
            return (arr[0].Type == JTokenType.String) &&
                   (arr[1].Type == JTokenType.Integer || arr[1].Type == JTokenType.Float);
        }
        return false;
    }

    /// <summary>Coerces a proposed token to the existing token type.</summary>
    private static JToken CoerceToExistingType(JToken existing, JToken proposed)
    {
        // If types are equal or proposed is null, just replace
        if (proposed.Type == existing.Type || proposed.Type == JTokenType.Null)
        {
            return proposed.DeepClone();
        }

        try
        {
            switch (existing.Type)
            {
                case JTokenType.Integer:
                    if (proposed.Type == JTokenType.Float) return new JValue((long)Math.Round(proposed.Value<double>()));
                    if (proposed.Type == JTokenType.String && long.TryParse(proposed.Value<string>(), out var li)) return new JValue(li);
                    if (proposed.Type == JTokenType.Boolean) return new JValue(proposed.Value<bool>() ? 1 : 0);
                    break;
                case JTokenType.Float:
                    if (proposed.Type == JTokenType.Integer) return new JValue((double)proposed.Value<long>());
                    if (proposed.Type == JTokenType.String && double.TryParse(proposed.Value<string>(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d)) return new JValue(d);
                    if (proposed.Type == JTokenType.Boolean) return new JValue(proposed.Value<bool>() ? 1.0 : 0.0);
                    break;
                case JTokenType.Boolean:
                    if (proposed.Type == JTokenType.String && bool.TryParse(proposed.Value<string>(), out var b)) return new JValue(b);
                    if (proposed.Type == JTokenType.Integer) return new JValue(proposed.Value<long>() != 0);
                    if (proposed.Type == JTokenType.Float) return new JValue(Math.Abs(proposed.Value<double>()) > double.Epsilon);
                    break;
                case JTokenType.String:
                    return new JValue(NormalizePotentialPath(proposed.ToString()));
                case JTokenType.Array:
                    if (proposed is JArray) return proposed.DeepClone();
                    break;
                case JTokenType.Object:
                    if (proposed is JObject) return proposed.DeepClone();
                    break;
            }
        }
        catch
        {
            // Fall-through to default (replace)
        }

        // Default behavior: replace
        return proposed.DeepClone();
    }

    // Report changed scalar inputs between workflow and summaries
    // Input: workflow (JObject), summariesJson (string of array [{title, inputs}])
    // Output: JArray of objects { title, param, value }
    /// <summary>Computes changed scalar values between workflow and summaries JSON.</summary>
    public static JArray GetChangedValues(JObject workflow, string summariesJson)
    {
        var result = new JArray();
        if (workflow is null || string.IsNullOrWhiteSpace(summariesJson)) return result;
        var summaries = JArray.Parse(summariesJson);

        foreach (var s in summaries.OfType<JObject>())
        {
            var title = s["title"]?.Value<string>();
            var desiredInputs = s["inputs"] as JObject;
            if (string.IsNullOrWhiteSpace(title) || desiredInputs is null) continue;

            var nodeProp = FindNodePropByVLTitle(workflow, title);
            if (nodeProp is null) continue;
            var node = (JObject)nodeProp.Value;
            var actualInputs = node["inputs"] as JObject ?? new JObject();

            foreach (var kv in desiredInputs.Properties())
            {
                var param = kv.Name;
                var desired = kv.Value;
                var actual = actualInputs[param];
                if (actual is null) continue; // strict: only existing keys
                if (IsConnection(actual)) continue; // skip connections

                if (!ScalarsEqual(actual, desired))
                {
                    result.Add(new JObject
                    {
                        ["title"] = title,
                        ["param"] = param,
                        ["value"] = NormalizeValueForOutput(desired)
                    });
                }
            }
        }

        return result;
    }

    // Convenience: return lines "title, param, value" for easy spread display
    /// <summary>Returns changed values as 'title, param, value' lines (JSON input).</summary>
    public static string[] GetChangedValuesLines(JObject workflow, string summariesJson)
    {
        var arr = GetChangedValues(workflow, summariesJson);
        var lines = new List<string>(arr.Count);
        foreach (var o in arr.OfType<JObject>())
        {
            var t = o["title"]?.Value<string>() ?? string.Empty;
            var p = o["param"]?.Value<string>() ?? string.Empty;
            var v = o["value"]?.ToString() ?? string.Empty;
            lines.Add(string.Concat(t, ", ", p, ", ", v));
        }
        return lines.ToArray();
    }

    // Overloads that accept JArray summaries directly
    /// <summary>Computes changed scalar values between workflow and summaries JArray.</summary>
    public static JArray GetChangedValues(JObject workflow, JArray summaries)
    {
        var result = new JArray();
        if (workflow is null || summaries is null) return result;
        foreach (var s in summaries.OfType<JObject>())
        {
            var title = s["title"]?.Value<string>();
            var desiredInputs = s["inputs"] as JObject;
            if (string.IsNullOrWhiteSpace(title) || desiredInputs is null) continue;

            var nodeProp = FindNodePropByVLTitle(workflow, title);
            if (nodeProp is null) continue;
            var node = (JObject)nodeProp.Value;
            var actualInputs = node["inputs"] as JObject ?? new JObject();

            foreach (var kv in desiredInputs.Properties())
            {
                var param = kv.Name;
                var desired = kv.Value;
                var actual = actualInputs[param];
                if (actual is null) continue;
                if (IsConnection(actual)) continue;
                if (!ScalarsEqual(actual, desired))
                {
                    result.Add(new JObject
                    {
                        ["title"] = title,
                        ["param"] = param,
                        ["value"] = NormalizeValueForOutput(desired)
                    });
                }
            }
        }
        return result;
    }

    /// <summary>Returns changed values as 'title, param, value' lines (JArray input).</summary>
    public static string[] GetChangedValuesLines(JObject workflow, JArray summaries)
    {
        var arr = GetChangedValues(workflow, summaries);
        var lines = new List<string>(arr.Count);
        foreach (var o in arr.OfType<JObject>())
        {
            var t = o["title"]?.Value<string>() ?? string.Empty;
            var p = o["param"]?.Value<string>() ?? string.Empty;
            var v = o["value"]?.ToString() ?? string.Empty;
            lines.Add(string.Concat(t, ", ", p, ", ", v));
        }
        return lines.ToArray();
    }

    /// <summary>Compares two scalar tokens with type-aware semantics.</summary>
    private static bool ScalarsEqual(JToken actual, JToken desired)
    {
        if (ReferenceEquals(actual, desired)) return true;
        if (actual is null || desired is null) return false;

        if ((actual.Type == JTokenType.Integer || actual.Type == JTokenType.Float) &&
            (desired.Type == JTokenType.Integer || desired.Type == JTokenType.Float || desired.Type == JTokenType.String))
        {
            double a;
            double d;
            try { a = actual.Type == JTokenType.Integer ? actual.Value<long>() : actual.Value<double>(); }
            catch { a = 0.0; }
            if (desired.Type == JTokenType.String)
            {
                if (!double.TryParse(desired.Value<string>(), System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out d)) return false;
            }
            else
            {
                d = desired.Type == JTokenType.Integer ? desired.Value<long>() : desired.Value<double>();
            }
            return Math.Abs(a - d) <= 1e-9;
        }

        if (actual.Type == JTokenType.Boolean)
        {
            bool ab = actual.Value<bool>();
            bool db;
            if (desired.Type == JTokenType.Boolean) db = desired.Value<bool>();
            else if (desired.Type == JTokenType.String && bool.TryParse(desired.Value<string>(), out var parsed)) db = parsed; else return false;
            return ab == db;
        }

        if (actual.Type == JTokenType.String && (desired.Type == JTokenType.String || desired.Type == JTokenType.Integer || desired.Type == JTokenType.Float || desired.Type == JTokenType.Boolean))
        {
            var a = NormalizePotentialPath(actual.Value<string>() ?? string.Empty);
            var d = NormalizePotentialPath(desired.ToString());
            return string.Equals(a, d, StringComparison.Ordinal);
        }

        return JToken.DeepEquals(actual, desired);
    }

    /// <summary>Normalizes a value for output (paths, numbers, booleans, scalars).</summary>
    private static JToken NormalizeValueForOutput(JToken value)
    {
        if (value is null) return JValue.CreateNull();
        if (value.Type == JTokenType.String)
        {
            return new JValue(NormalizePotentialPath(value.Value<string>() ?? string.Empty));
        }
        if (value.Type == JTokenType.Float)
        {
            return new JValue(value.Value<double>());
        }
        if (value.Type == JTokenType.Integer)
        {
            return new JValue(value.Value<long>());
        }
        if (value.Type == JTokenType.Boolean)
        {
            return new JValue(value.Value<bool>());
        }
        return new JValue(value.ToString(Formatting.None));
    }

    // Path normalization: avoid double escaping and prefer forward slashes for JSON/Comfy
    /// <summary>Normalizes potential file paths (collapse backslashes, use '/').</summary>
    private static string NormalizePotentialPath(string s)
    {
        if (string.IsNullOrEmpty(s)) return string.Empty;
        // Collapse double backslashes first
        var collapsed = s.Replace("\\\\", "\\");
        // Prefer forward slashes to avoid escaping issues
        var normalized = collapsed.Replace('\\', '/');
        return normalized;
    }

    // List all scalar VL parameters as lines: "title, param, value"
    // - Skips connection arrays
    // - Normalizes string paths
    // - Returns lines sorted alphabetically
    /// <summary>Lists all scalar VL params as 'title, param, value' lines.</summary>
    private static string[] ListVLParamsLines(JObject workflow)
    {
        var lines = new List<string>();
        if (workflow is null) return lines.ToArray();

        foreach (var prop in workflow.Properties())
        {
            if (prop.Value is not JObject node) continue;
            var meta = node["_meta"] as JObject;
            var title = meta? ["title"]?.Value<string>() ?? string.Empty;
            if (string.IsNullOrEmpty(title) || !title.StartsWith("VL ", StringComparison.Ordinal)) continue;

            var inputs = node["inputs"] as JObject ?? new JObject();
            foreach (var kv in inputs.Properties())
            {
                var key = kv.Name;
                var value = kv.Value;

                if (IsConnection(value)) continue;

                string valueString = string.Empty;
                if (value is null || value.Type == JTokenType.Null)
                {
                    valueString = string.Empty;
                }
                else if (value.Type == JTokenType.String)
                {
                    valueString = NormalizePotentialPath(value.Value<string>() ?? string.Empty);
                }
                else if (value.Type == JTokenType.Integer)
                {
                    valueString = value.Value<long>().ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (value.Type == JTokenType.Float)
                {
                    valueString = value.Value<double>().ToString(System.Globalization.CultureInfo.InvariantCulture);
                }
                else if (value.Type == JTokenType.Boolean)
                {
                    valueString = value.Value<bool>() ? "true" : "false";
                }
                else
                {
                    // Skip arrays/objects and other non-scalar types
                    continue;
                }

                lines.Add(string.Concat(title, ", ", key, ", ", valueString));
            }
        }

        lines.Sort(StringComparer.Ordinal);
        
        return lines.ToArray();
    }

    /// <summary>Lists scalar VL params returning both lines array and joined string.</summary>
    public static void ListVLParams(JObject workflow, out string[] lines, out string text)
    {
        lines = ListVLParamsLines(workflow);
        text = string.Join(Environment.NewLine, lines);
    }

    // ====================
    // Comfy Queue status helpers
    // ====================
    /// <summary>
    /// Parses a ComfyUI queue JSON object and returns counts and summaries.
    /// Outputs:
    /// - pendingCount: number of items in 'queue_pending'
    /// - runningCount: number of items in 'queue_running'
    /// - runningLines: short descriptions of running entries
    /// - report: joined string for display
    /// </summary>
    public static void QueueStatus(JObject queue,
        out int pendingCount,
        out int runningCount,
        out string[] runningLines,
        out string report)
    {
        pendingCount = 0;
        runningCount = 0;
        runningLines = Array.Empty<string>();
        report = string.Empty;
        if (queue is null) return;

        var pending = queue["queue_pending"] as JArray;
        var running = queue["queue_running"] as JArray;
        pendingCount = pending?.Count ?? 0;
        runningCount = running?.Count ?? 0;

        var lines = new List<string>();
        if (running != null)
        {
            foreach (var item in running)
            {
                // Comfy running entry is typically an array: [batch, id, promptObj, extra, ...]
                if (item is JArray arr)
                {
                    string id = arr.Count > 1 ? arr[1]?.ToString() ?? string.Empty : string.Empty;
                    var promptObj = arr.Count > 2 ? arr[2] as JObject : null;
                    int nodeCount = promptObj?.Properties().Count() ?? 0;
                    // Try to gather up to 3 titles from _meta.title or fallback to type
                    var titles = new List<string>();
                    if (promptObj != null)
                    {
                        foreach (var p in promptObj.Properties())
                        {
                            if (p.Value is JObject n)
                            {
                                var meta = n["_meta"] as JObject;
                                var title = meta? ["title"]?.Value<string>();
                                if (string.IsNullOrWhiteSpace(title))
                                {
                                    title = n["class_type"]?.Value<string>() ?? string.Empty;
                                }
                                if (!string.IsNullOrWhiteSpace(title)) titles.Add(title);
                            }
                            if (titles.Count >= 3) break;
                        }
                    }
                    var titleList = titles.Count > 0 ? string.Join(", ", titles) : "—";
                    lines.Add(string.Concat("id=", id, ", nodes=", nodeCount.ToString(), ", titles=", titleList));
                }
                else
                {
                    lines.Add(item.ToString(Formatting.None));
                }
            }
        }

        runningLines = lines.ToArray();
        report = lines.Count > 0 ? string.Join(Environment.NewLine, lines) : string.Empty;
    }

    /// <summary>
    /// Overload: accepts raw queue JSON string.
    /// </summary>
    public static void QueueStatus(string queueJson,
        out int pendingCount,
        out int runningCount,
        out string[] runningLines,
        out string report)
    {
        pendingCount = 0;
        runningCount = 0;
        runningLines = Array.Empty<string>();
        report = string.Empty;
        if (string.IsNullOrWhiteSpace(queueJson)) return;
        try
        {
            var obj = JObject.Parse(queueJson);
            QueueStatus(obj, out pendingCount, out runningCount, out runningLines, out report);
        }
        catch
        {
            // keep defaults
        }
    }

    // ====================
    // ComfyInspector helpers
    // ====================
    /// <summary>Returns all node titles starting with 'VL ' for a dropdown.</summary>
    public static string[] GetVLNodeTitles(JObject workflow)
    {
        if (workflow is null) return Array.Empty<string>();
        var titles = new List<string>();
        foreach (var prop in workflow.Properties())
        {
            if (prop.Value is not JObject node) continue;
            var meta = node["_meta"] as JObject;
            var title = meta? ["title"]?.Value<string>() ?? string.Empty;
            if (title.StartsWith("VL ", StringComparison.Ordinal)) titles.Add(title);
        }
        titles.Sort(StringComparer.Ordinal);
        return titles.ToArray();
    }

    /// <summary>
    /// Inspect a single VL node by title. Outputs inputs JObject and formatted lines "param, value".
    /// </summary>
    public static void InspectVLNode(JObject workflow, string title, out JObject inputs, out string[] lines, out string report)
    {
        inputs = new JObject();
        lines = Array.Empty<string>();
        report = string.Empty;
        if (workflow is null || string.IsNullOrWhiteSpace(title)) return;

        var nodeProp = FindNodePropByVLTitle(workflow, title);
        if (nodeProp is null) return;
        var node = (JObject)nodeProp.Value;
        inputs = node["inputs"] as JObject ?? new JObject();

        var list = new List<string>();
        foreach (var kv in inputs.Properties())
        {
            var key = kv.Name;
            var value = kv.Value;
            if (IsConnection(value)) continue;

            string valueString = string.Empty;
            if (value is null || value.Type == JTokenType.Null)
            {
                valueString = string.Empty;
            }
            else if (value.Type == JTokenType.String)
            {
                valueString = NormalizePotentialPath(value.Value<string>() ?? string.Empty);
            }
            else if (value.Type == JTokenType.Integer)
            {
                valueString = value.Value<long>().ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (value.Type == JTokenType.Float)
            {
                valueString = value.Value<double>().ToString(System.Globalization.CultureInfo.InvariantCulture);
            }
            else if (value.Type == JTokenType.Boolean)
            {
                valueString = value.Value<bool>() ? "true" : "false";
            }
            else
            {
                continue;
            }

            list.Add(string.Concat(key, ", ", valueString));
        }

        list.Sort(StringComparer.Ordinal);
        lines = list.ToArray();
        report = string.Join(Environment.NewLine, lines);
    }

    // ====================
    // Comfy Workflow (nodes array) inspector helpers
    // ====================
    private static string ExtractNodeTitle(JObject node)
    {
        if (node is null) return string.Empty;
        // 1. Direct "title" property (manually set in UI)
        var title = node["title"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(title)) return title;

        // 2. Meta title (used in prompt/API format)
        var meta = node["_meta"] as JObject;
        title = meta?["title"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(title)) return title;

        // 3. S&R title (from workflow format properties)
        var props = node["properties"] as JObject;
        title = props?["Node name for S&R"]?.Value<string>();
        if (!string.IsNullOrWhiteSpace(title)) return title;

        // 4. Fallback to node type name
        return node["type"]?.Value<string>() ?? node["class_type"]?.Value<string>() ?? string.Empty;
    }

    private static JObject? FindNodeByTitle(JObject workflow, string titleToFind)
    {
        if (workflow is null || string.IsNullOrWhiteSpace(titleToFind)) return null;

        // Case 1: Standard workflow with "nodes" array
        if (workflow["nodes"] is JArray nodesArray)
        {
            foreach (var node in nodesArray.OfType<JObject>())
            {
                if (string.Equals(ExtractNodeTitle(node), titleToFind, StringComparison.Ordinal))
                    return node;
            }
        }
        // Case 2: Prompt format with nodes as direct properties
        else
        {
            foreach (var prop in workflow.Properties())
            {
                if (prop.Value is JObject node)
                {
                    if (string.Equals(ExtractNodeTitle(node), titleToFind, StringComparison.Ordinal))
                        return node;
                }
            }
        }
        return null;
    }
    
    /// <summary>
    /// Returns all workflow node titles from a ComfyUI workflow JSON that uses the 'nodes' array structure.
    /// </summary>
    public static string[] GetWorkflowNodeTitles(JObject workflow)
    {
        if (workflow is null) return Array.Empty<string>();
        var titles = new HashSet<string>();

        // Case 1: Standard workflow with "nodes" array
        if (workflow["nodes"] is JArray nodesArray)
        {
            foreach (var node in nodesArray.OfType<JObject>())
            {
                var title = ExtractNodeTitle(node);
                if (!string.IsNullOrWhiteSpace(title))
                    titles.Add(title);
            }
        }
        // Case 2: Prompt format with nodes as direct properties
        else
        {
            foreach (var prop in workflow.Properties())
            {
                if (prop.Value is JObject node)
                {
                    var title = ExtractNodeTitle(node);
                    if (!string.IsNullOrWhiteSpace(title))
                        titles.Add(title);
                }
            }
        }

        var sortedTitles = titles.ToList();
        sortedTitles.Sort(StringComparer.Ordinal);
        return sortedTitles.ToArray();
    }

    /// <summary>
    /// Inspect a workflow node by title and return a flattened parameters object based on widgets_values and properties.
    /// Tries to map widget indices to known parameter names by node 'type' (Node name for S&R).
    /// </summary>
    public static void InspectWorkflowNode(JObject workflow, string title, out JObject parameters, out string[] lines, out string report)
    {
        parameters = new JObject();
        lines = Array.Empty<string>();
        report = string.Empty;
        if (workflow is null || string.IsNullOrWhiteSpace(title)) return;

        var node = FindNodeByTitle(workflow, title);
        if (node is null) return;

        var paramMap = MapWorkflowNodeParameters(node);
        parameters = paramMap;

        var list = new List<string>();
        foreach (var kv in paramMap.Properties())
        {
            list.Add(string.Concat(kv.Name, ", ", kv.Value?.ToString() ?? string.Empty));
        }
        list.Sort(StringComparer.Ordinal);
        lines = list.ToArray();
        report = string.Join(Environment.NewLine, lines);
    }

    private static JObject MapWorkflowNodeParameters(JObject node)
    {
        var result = new JObject();
        if (node is null) return result;

        var props = node["properties"] as JObject ?? new JObject();
        var typeName = props["Node name for S&R"]?.Value<string>() ?? node["type"]?.Value<string>() ?? node["class_type"]?.Value<string>() ?? string.Empty;

        // Case 1: Standard workflow format with "widgets_values" array
        if (node["widgets_values"] is JArray widgets)
        {
            // Known widget parameter name mappings
            var known = new Dictionary<string, string[]>(StringComparer.Ordinal)
            {
                { "CheckpointLoaderSimple", new [] { "ckpt_name" } },
                { "EmptyLatentImage", new [] { "width", "height", "batch_size" } },
                { "CLIPTextEncode", new [] { "text" } },
                { "KSampler", new [] { "seed", "seed_behavior", "steps", "cfg", "sampler_name", "scheduler", "denoise" } },
                { "SaveImage", new [] { "filename_prefix" } },
            };

            if (known.TryGetValue(typeName, out var names))
            {
                for (int i = 0; i < widgets.Count; i++)
                {
                    var key = i < names.Length ? names[i] : ("widget_" + i);
                    result[key] = NormalizeValueForOutput(widgets[i]);
                }
            }
            else
            {
                for (int i = 0; i < widgets.Count; i++)
                {
                    result["widget_" + i] = NormalizeValueForOutput(widgets[i]);
                }
            }
        }
        // Case 2: Prompt/API format with "inputs" object
        else if (node["inputs"] is JObject inputs)
        {
            foreach (var kv in inputs.Properties())
            {
                // A JValue is a scalar (string, number, bool, null)
                // This check filters out connections, which are JArrays
                if (kv.Value is JValue)
                {
                    result[kv.Name] = NormalizeValueForOutput(kv.Value);
                }
            }
        }

        // Include a few useful properties for context (version, type)
        if (!string.IsNullOrEmpty(typeName)) result["type"] = typeName;
        var ver = props["ver"]?.ToString();
        if (!string.IsNullOrEmpty(ver)) result["version"] = ver;

        return result;
    }

    // (old ComfyPngParser node removed; use ComfyPngUtils instead)
}