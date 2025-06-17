using OpenAI;
using OpenAI.Chat;
using System;
using System.ClientModel;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using System.Text;
using System.Text.Json;

namespace AIDiscuss
{
    internal class Program
    {
        public static List<ChatMessage> ChatMessagesA { get; set; } = [];

        public static List<ChatMessage> ChatMessagesB { get; set; } = [];

        private static void Main(string[] args)
        {
            string endPoint1 = "https://api.deepseek.com";
            string endPoint2 = "https://dashscope.aliyuncs.com/compatible-mode/v1";

            string token1 = "";
            string token2 = "";

            string model1 = "deepseek-reasoner";
            string model2 = "qwen-plus";

            string name1 = "D老师";
            string name2 = "Q老师";

            string subject = "不用华为手机就相当于不爱国";

            string prompt1 = $"本轮的讨论议题是：{subject}。**禁止使用MarkDown语法**，语言精炼、论点真实，不得捏造，**不得多于500字**。内容需要脚踏实地，禁止幻想。你在本场中的目的是支持这个论点。请分辨对方论点是否真实，虚假论点可以作为攻击武器。如果你被对方的观点驳倒或无法回答，请只回复`<Finish>`，注意：你替对方回答`<Finish>`，只会算你认输";
            string prompt2 = $"本轮的讨论议题是：{subject}。**禁止使用MarkDown语法**，语言精炼、论点真实，不得捏造，**不得多于500字**。内容需要脚踏实地，禁止幻想。你在本场中的目的是反对这个论点。请分辨对方论点是否真实，虚假论点可以作为攻击武器。如果你被对方的观点驳倒或无法回答，请只回复`<Finish>`，注意：你替对方回答`<Finish>`，只会算你认输";
            ChatMessagesA.Add(new SystemChatMessage(prompt1));
            ChatMessagesB.Add(new SystemChatMessage(prompt2));
            int roundMax = 10;

            int tokenConsumeA = 0;
            int tokenConsumeB = 0;
            int round = 0;
            using FileStream file = new($"DiscussHistory{DateTime.Now:yyyyMMddHHmmss}.md", FileMode.CreateNew, FileAccess.Write, FileShare.Read);
            file.Write(Encoding.UTF8.GetBytes($"# 本轮辩论\n\n- 辩论主题：{subject}\n\n- 正方：{model1} - {name1}\n\n- 反方：{model2} - {name2}\n\n"));
            file.Flush();
            Console.WriteLine($"本轮辩论主题：{subject}");
            Console.WriteLine();
            while (true)
            {
                round++;
                if (round >= roundMax)
                {
                    Console.WriteLine($"本局辩论结束，已达到最大轮次{roundMax}，请重新开始。");
                    break;
                }
                Console.Title = $"第{round}轮 - {subject}";

                Console.Write($"{name1}：");
                (bool finish, string reasoningResult, string result, int token) = Chat(name1, endPoint1, token1, model1, ChatMessagesA);
                tokenConsumeA += token;
                file.Write(Encoding.UTF8.GetBytes($"# Round {round} - {name1}\n\n" +
                    $"{(string.IsNullOrEmpty(reasoningResult) ? "" : $"> 思考：{reasoningResult}\n\n")}{result}\n\n"));
                file.Flush();
                ChatMessagesA.Add(new AssistantChatMessage(result));
                ChatMessagesB.Add(new UserChatMessage(result));
                if (finish)
                {
                    if (!HandleCanFinish(name1))
                    {
                        ChatMessagesA.Add(new UserChatMessage("裁判：你的观点不足以判定对方为负，随意使用结束符，扣一分"));
                        (finish, reasoningResult, result, token) = Chat(name1, endPoint1, token1, model1, ChatMessagesA);
                        ChatMessagesA.Add(new AssistantChatMessage(result));
                        ChatMessagesB.Add(new UserChatMessage(result));
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("反方辩论成功");
                        file.Write(Encoding.UTF8.GetBytes($"> {name1} 已认输\n"));
                        break;
                    }
                }
                Console.WriteLine();
                Console.WriteLine();

                Console.Write($"{name2}：");
                (finish, reasoningResult, result, token) = Chat(name2, endPoint2, token2, model2, ChatMessagesB);
                tokenConsumeB += token;
                file.Write(Encoding.UTF8.GetBytes($"# Round {round} - {name2}\n\n" +
                    $"{(string.IsNullOrEmpty(reasoningResult) ? "" : $"> 思考：{reasoningResult}\n\n")}{result}\n\n"));
                file.Flush();
                ChatMessagesB.Add(new AssistantChatMessage(result));
                ChatMessagesA.Add(new UserChatMessage(result));
                if (finish)
                {
                    if (!HandleCanFinish(name1))
                    {
                        ChatMessagesB.Add(new UserChatMessage("裁判：你的观点不足以判定对方为负，随意使用结束符，扣一分"));
                        (finish, reasoningResult, result, token) = Chat(name2, endPoint2, token2, model2, ChatMessagesB);
                        ChatMessagesB.Add(new AssistantChatMessage(result));
                        ChatMessagesA.Add(new UserChatMessage(result));
                    }
                    else
                    {
                        Console.WriteLine();
                        Console.WriteLine("正方辩论成功");
                        file.Write(Encoding.UTF8.GetBytes($"> {name2} 已认输\n"));
                        break;
                    }
                }
                Console.WriteLine();
                Console.WriteLine();
            }
            file.Write(Encoding.UTF8.GetBytes($"# 辩论结束\n\n共进行了{round}轮，{model1}消耗Token：{tokenConsumeA}；{model2}消耗Token：{tokenConsumeB}；\n\n"));
            file.Flush();
            Console.WriteLine($"本局辩论结束，共进行了{round}轮，{model1}消耗Token：{tokenConsumeA}；{model2}消耗Token：{tokenConsumeB}；");
            Console.WriteLine($"辩论结果已保存到文件：{file.Name}。请按任意键退出。");
            Console.ReadKey();
        }

        private static (bool finish, string reasoningResult, string result, int token) Chat(string name, string url, string token, string modelName, List<ChatMessage> chatMessages)
        {
            var c = new OpenAIClient(new ApiKeyCredential(token), new OpenAIClientOptions() { Endpoint = new(url), NetworkTimeout = TimeSpan.FromSeconds(30) });
            var client = c.GetChatClient(modelName);
            var option = new ChatCompletionOptions
            {
                MaxOutputTokenCount = 3000,
                Temperature = 1f,
            };
            option.Tools.Add(ChatTool.CreateFunctionTool("Finish", "此函数表示你被对方的观点说服，当你被对方论点驳倒无法回复时调用此函数来结束本局辩论"));
            bool requiresAction;
            string msg = "";
            string reasoningResult = "";
            int tokenConsume = 0;
            bool hasReasoning = false;
            try
            {
                do
                {
                    requiresAction = false;
                    List<ChatToolCall> toolCall = [];
                    ChatFinishReason finishReason = ChatFinishReason.Stop;
                    foreach (StreamingChatCompletionUpdate chatUpdate in client.CompleteChatStreaming(chatMessages, option))
                    {
                        var delta = AppendContentToMessage(chatUpdate.ContentUpdate);
                        if (!string.IsNullOrEmpty(delta))
                        {
                            msg += delta;
                            if (hasReasoning)
                            {
                                Console.WriteLine();
                                hasReasoning = false;
                            }
                            Console.Write(delta);
                        }
                        delta = AppendReasoningContentToMessage(chatUpdate);
                        if (!string.IsNullOrEmpty(delta))
                        {
                            reasoningResult += delta;
                            if (!hasReasoning)
                            {
                                Console.Write("\n思考: ");
                                hasReasoning = true;
                            }
                            Console.Write(delta);
                        }
                        // TODO: tool stream update
                        finishReason = chatUpdate.FinishReason ?? ChatFinishReason.Stop;
                        tokenConsume += chatUpdate.Usage?.InputTokenCount ?? 0;
                        tokenConsume += chatUpdate.Usage?.OutputTokenCount ?? 0;
                    }
                    switch (finishReason)
                    {
                        case ChatFinishReason.Stop:
                            break;

                        case ChatFinishReason.ToolCalls:
                            Console.WriteLine("尝试 ToolCall");
                            foreach (var tool in toolCall)
                            {
                                switch (tool.FunctionName)
                                {
                                    case "Finish":
                                        return (true, reasoningResult, "我甘拜下风。", tokenConsume);
                                }
                            }
                            requiresAction = true;
                            break;

                        case ChatFinishReason.ContentFilter:
                            return (false, reasoningResult, "因触发内容过滤，我无法回答。", tokenConsume);
                    }
                } while (requiresAction);
            }
            catch (Exception e)
            {
                Debugger.Break();
                Console.WriteLine($"发生异常：{e}");
                return (true, "", "发生异常，无法继续辩论。", 0);
            }
            if (string.IsNullOrEmpty(msg))
            {
                Debugger.Break();
                msg = "我因网络原因无法回答<Finish>";
                Console.WriteLine("没有收到任何回复，请检查网络连接或API密钥是否正确。");
            }
            return (msg.Contains("<Finish>"), reasoningResult, msg, tokenConsume);
        }

        private static string AppendReasoningContentToMessage(StreamingChatCompletionUpdate chatUpdate)
        {
            var choicesProp = chatUpdate?.GetType().GetProperty(
                "Choices",
                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
            );
            if (choicesProp == null)
            {
                return "";
            }

            if (choicesProp.GetValue(chatUpdate) is not IEnumerable choices)
            {
                return "";
            }

            var result = new List<string>();

            foreach (var choice in choices)
            {
                if (choice == null)
                {
                    continue;
                }

                var deltaProp = choice.GetType().GetProperty(
                    "Delta",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (deltaProp == null)
                {
                    continue;
                }

                var delta = deltaProp.GetValue(choice);
                if (delta == null)
                {
                    continue;
                }

                var rawDataProp = delta.GetType().GetProperty(
                    "SerializedAdditionalRawData",
                    BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public
                );
                if (rawDataProp == null)
                {
                    continue;
                }

                if (rawDataProp.GetValue(delta) is not IDictionary<string, BinaryData> rawData)
                {
                    continue;
                }

                foreach (var kvp in rawData)
                {
                    if (kvp.Key == "reasoning_content")
                    {
                        return JsonSerializer.Deserialize<string>(Encoding.UTF8.GetString(kvp.Value.ToArray())) ?? "";
                    }
                }
            }

            return string.Join("\n", result);
        }

        private static string AppendContentToMessage(ChatMessageContent contents)
        {
            string msg = "";
            foreach (ChatMessageContentPart contentPart in contents)
            {
                msg += contentPart.Text;
            }
            return msg;
        }

        private static bool HandleCanFinish(string name)
        {
            Console.Beep();
            Console.WriteLine($"{name} 认为对方观点正确，裁判判定能否正常结束(Y/N)");
            var c = Console.Read();
            if (c == 'y' || c == 'Y')
            {
                return true;
            }
            else
            {
                return false;
            }
        }
    }
}
