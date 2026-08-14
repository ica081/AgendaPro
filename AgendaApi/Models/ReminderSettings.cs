using System.Text.Json.Serialization;

namespace AgendaApi.Models;

public class ReminderSettings
{
    [JsonPropertyName("enabled")]
    public bool Enabled { get; set; } = false;

    [JsonPropertyName("offsetValue")]
    public int OffsetValue { get; set; } = 1; // ex: 1, 24, 48

    [JsonPropertyName("offsetUnit")]
    public string OffsetUnit { get; set; } = "Hours"; // "Hours" ou "Days"

    [JsonPropertyName("sendTime")]
    public string SendTime { get; set; } = "BeforeAppointment"; // "BeforeAppointment" ou "DayOfAppointment"
}