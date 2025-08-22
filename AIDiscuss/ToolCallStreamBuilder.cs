using OpenAI.Chat;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AIDiscuss
{
    public class ToolCallStreamBuilder
    {
        private Dictionary<int, string> ToolCallId { get; set; } = [];

        private Dictionary<int, string> ToolCallFunctionName { get; set; } = [];

        private Dictionary<int, byte[]> ToolCallFunctionArguments { get; set; } = [];

        public void Append(IReadOnlyList<StreamingChatToolCallUpdate> toolCallUpdates)
        {
            foreach (var item in toolCallUpdates)
            {
                if (item.ToolCallId != null)
                {
                    ToolCallId[item.Index] = item.ToolCallId;
                }
                if (item.FunctionName != null)
                {
                    ToolCallFunctionName[item.Index] = item.FunctionName;
                }
                if (item.FunctionArgumentsUpdate != null)
                {
                    if (ToolCallFunctionArguments.TryGetValue(item.Index, out var value))
                    {
                        ToolCallFunctionArguments[item.Index] = [.. value, .. item.FunctionArgumentsUpdate.ToArray()];
                    }
                    else
                    {
                        ToolCallFunctionArguments[item.Index] = [.. item.FunctionArgumentsUpdate.ToArray()];
                    }
                }
            }
        }

        public List<ChatToolCall> Build()
        {
            List<ChatToolCall> toolCalls = [];
            foreach (var item in ToolCallId)
            {
                var index = item.Key;
                var id = item.Value;
                var functionName = ToolCallFunctionName[index];
                var argument = ToolCallFunctionArguments[index];

                var toolCall = ChatToolCall.CreateFunctionToolCall(id, functionName, BinaryData.FromBytes(argument));
                toolCalls.Add(toolCall);
            }
            return toolCalls;
        }
    }
}
