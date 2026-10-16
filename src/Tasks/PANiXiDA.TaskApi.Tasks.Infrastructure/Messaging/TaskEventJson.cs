using System.Text.Json;
using System.Text.Json.Serialization;

namespace PANiXiDA.TaskApi.Tasks.Infrastructure.Messaging;

internal static class TaskEventJson
{
    internal static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };
}
