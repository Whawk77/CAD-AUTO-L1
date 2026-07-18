using System.Text;
using System.Text.Json;

namespace CatiaAiPanel.Core;

public sealed class PromptBuilder(ICatiaContextSerializer serializer)
{
    public string BuildInitial(string userRequest, CatiaContextSnapshot context)
    {
        var prompt = new StringBuilder(CommonRules);
        prompt.AppendLine().AppendLine("## 用户请求").AppendLine(userRequest.Trim());
        prompt.AppendLine().AppendLine("## 当前 CATIA 上下文 JSON").AppendLine("```json")
            .AppendLine(serializer.Serialize(context)).AppendLine("```");
        return prompt.ToString();
    }

    public string BuildCorrection(
        ConversationState state,
        string userFeedback,
        CatiaContextSnapshot context)
    {
        var prompt = new StringBuilder(CommonRules);
        prompt.AppendLine().AppendLine("## 修正任务");
        prompt.AppendLine(string.IsNullOrWhiteSpace(userFeedback)
            ? "上一版执行失败。根据执行结果和最新上下文生成修正版。"
            : userFeedback.Trim());
        prompt.AppendLine().AppendLine("## CATIA Sketcher 兼容规则");
        prompt.AppendLine("- Constraints.AddMonoEltCst 的元素参数直接传 Factory2D 创建的 Line2D/Circle2D 等二维几何对象，不要先调用 Part.CreateReferenceFromObject。 ");
        prompt.AppendLine("- 如果草图尺寸约束继续返回对象错误，优先保留已测数值创建的草图与 PartDesign 特征；可以把语义 Parameters 作为记录参数，并在 warnings 中声明草图轮廓需重新生成，不要因非必要公式让整个新 Part 为空。 ");
        prompt.AppendLine().AppendLine("## 上一版宏").AppendLine("```vb")
            .AppendLine(state.CurrentProposal?.Macro.Code ?? "").AppendLine("```");
        prompt.AppendLine().AppendLine("## 上次执行结果 JSON").AppendLine("```json")
            .AppendLine(JsonSerializer.Serialize(state.LastExecution, JsonOptions)).AppendLine("```");
        prompt.AppendLine().AppendLine("## 最新 CATIA 上下文 JSON").AppendLine("```json")
            .AppendLine(serializer.Serialize(context)).AppendLine("```");
        return prompt.ToString();
    }

    public string BuildReconstruction(CatiaContextSnapshot context, string userIntent = "", bool allowDimensionInference = true)
    {
        var prompt = new StringBuilder(CommonRules);
        prompt.AppendLine().AppendLine("## 专用任务：测量驱动的参数化逆向建模");
        prompt.AppendLine("审查 measurements 与 reconstructionReadiness，目标是在不修改当前参考 3DXML/CGR 的前提下，新建一个 CATPart 并参数化重建目标零件。");
        if (!string.IsNullOrWhiteSpace(userIntent))
            prompt.AppendLine().AppendLine("## 用户补充的设计意图").AppendLine(userIntent.Trim());
        prompt.AppendLine().AppendLine("## 决策规则");
        if (allowDimensionInference)
        {
            prompt.AppendLine("- 当前启用‘自动补齐未测尺寸’。用户确认的语义测量是硬约束；缺失值依次通过几何恒等式、对称/阵列关系、已测坐标跨度和保守比例推导。 ");
            prompt.AppendLine("- 只要 overall_length、overall_width、overall_height 都存在且为正数，就生成可预览的新 CATPart 宏；不得仅因缺少局部基准、孔径、槽端半径或特征位置而返回 needsClarification。 ");
            prompt.AppendLine("- 自动补齐只适用于账本已有语义证据的特征。不得凭空增加未测得、未命名的孔、槽、圆角或台阶。 ");
            prompt.AppendLine("- 每个补齐值必须创建为名称以 AI_INFERRED_ 开头的 CATIA Parameter；reconstructionPlan.assumptions 必须逐项写明数值、公式、基准和用途。assistantMessage 必须说明这是近似重建。 ");
            prompt.AppendLine("- 默认局部基准：主体左下角为 (0,0,0)，X=总长，Y=总宽，Z=总高；未定位的单个特征置于对应面的中心，成组特征按已测孔距/间距关于中心对称。 ");
            prompt.AppendLine().AppendLine("## 自动补齐建议");
            foreach (var suggestion in DimensionInferencePlanner.Build(context.Measurements))
                prompt.AppendLine("- " + suggestion);
        }
        else
        {
            prompt.AppendLine("- 尺寸、局部基准、截面/特征顺序或语义不足时：needsClarification=true，reconstructionPlan.readyToModel=false，code 为空；missingMeasurements 必须逐项说明下一步在 CATIA 中应测什么、相对哪个基准测、为什么需要。 ");
        }
        prompt.AppendLine("- 信息足够时：operationKind=write，targetDocumentMode=newPart，reconstructionPlan.readyToModel=true；宏必须用 CATIA.Documents.Add(\"Part\") 新建目标文档。 ");
        prompt.AppendLine("- 新 CATPart 中先创建带语义名称的 Length/Angle/Real Parameters，再创建草图约束、Pad/Pocket/Hole/Fillet 等特征，并引用这些 Parameters。 ");
        prompt.AppendLine(allowDimensionInference
            ? "- 绝不修改、保存或关闭当前参考文档；允许的补齐值必须按上述规则显式标为 AI_INFERRED_*，不得伪装成实测值。 "
            : "- 绝不修改、保存或关闭当前参考文档；不得猜测未测得尺寸。 ");
        prompt.AppendLine().AppendLine("## 当前 CATIA 上下文 JSON").AppendLine("```json")
            .AppendLine(serializer.Serialize(context)).AppendLine("```");
        return prompt.ToString();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true
    };

    private const string CommonRules = """
    你是 CATIA V5 Automation 宏工程师。只生成 CATScript/VBScript。生成目标版本必须以当前上下文的 catiaVersion 为准；没有版本信息时采用 V5-6R2018（B28）的保守 API 子集。

    必须遵守：
    1. 入口固定为 Function AIEntry(contextJson)，并返回 JSON 字符串，字段为 success、summary、warnings、diagnostics。
    2. 只能操作当前已运行的 CATIA；不要 CreateObject/GetObject。targetDocumentMode=currentDocument 时只操作当前文档；targetDocumentMode=newPart 时只允许通过 CATIA.Documents.Add("Part") 新建目标文档并操作该新文档，不得修改参考文档。
    3. 禁止 Shell、外部进程、网络、注册表和文件系统访问。
    4. 禁止 CATIA.Quit、关闭文档、SaveAs、ExecuteScript、Evaluate、StartCommand。
    5. 不要弹出 MsgBox/InputBox；结果通过 AIEntry 返回值报告。
    6. 不确定对象类型或关键参数时 needsClarification=true，code 设为空字符串，不要猜测执行；唯一例外是专用重建任务明确启用“自动补齐未测尺寸”，此时只能按任务中的补齐规则生成新 CATPart，并显式标记 AI_INFERRED_*。
    7. operationKind 必须准确标记 read 或 write；targetDocumentMode 必须准确标记 currentDocument 或 newPart。写入当前文档要求文档已保存；新建 Part 不要求参考文档可写，但仍需用户逐版确认。
    8. 使用 On Error 的范围必须尽量小，不能用全局 Resume Next 隐藏失败；发生错误时返回 success=false 和明确 diagnostics。
    9. 代码应有限循环，不依赖 CATIA Drafting、CAA、VBA 工程、Python 或第三方库。
    10. 输出必须严格符合给定 JSON Schema，不要添加 Markdown 包裹。
    11. CATIA V5 Automation 没有通用的 Product.GetBoundingBox、Analyze.GetBoundingBox 或 Measurable.GetBoundingBox；禁止生成这些调用。
    12. 如果选择对象名称以 .cgr 结尾，或对象仅来自 3DXML/CGR 可视化表示，则 Automation 无法读取 Part.Bodies、孔特征和精确外包络。默认要求完整语义账本；专用重建任务启用自动补齐且总长/总宽/总高齐全时，可以按显式假设新建近似 Part，但仍不得把 CGR 当作可编辑 Part。
    13. 如果用户要求的尺寸一个都没有测得，运行结果必须 success=false，不能以 warning 代替失败。
    14. 上下文的 measurements 是用户确认并记录的建模测量账本。重建 Part 时其中的 value、unit、coordinates、direction 和 source 是硬约束；只有专用重建任务明确启用自动补齐时才可推导缺失尺寸，且必须以 AI_INFERRED_* 参数和 assumptions 明示，不能伪装成实测值。
    15. measurements 中名称类似 Distance.1/Selection.1 时语义仍不明确；在生成写宏前应要求用户确认它对应总长、宽、高、孔径、孔距等哪一种设计意图。
    16. 写入新 CATPart 时，把已确认的测量值创建为带语义名称的 Parameters，并让草图约束/特征尺寸引用这些 Parameters；不要只把数值硬编码在宏语句中。
    17. 每次都必须返回 reconstructionPlan。非重建任务使用 readyToModel=false、strategy="not-applicable"、missingMeasurements=[]；重建任务必须如实列出准备度和缺失尺寸。
    18. CATScript 标识符使用 ASCII；代码、注释和返回字符串不得包含 emoji 或当前 Windows ANSI 代码页难以表示的符号。
    19. targetDocumentMode=newPart 时必须明确创建新 CATPart；在 CATIA.Documents.Add("Part") 之前不得取得 ActiveDocument，且不得调用参考文档的 Part.Update、Selection.Delete、Save 或任何修改方法。
    20. 创建 LENGTH/ANGLE 参数时不要把显示单位数值直接作为 CreateDimension 的初值。先用 0 创建，再用 ValuateFromString("75mm")、ValuateFromString("30deg") 这类带单位字符串赋值，避免把毫米误作米。
    """;
}
