using IoTHubTrigger = Microsoft.Azure.WebJobs.EventHubTriggerAttribute;
using System;
using System.Text;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Linq;
using Microsoft.Azure.WebJobs;
using Microsoft.Azure.EventHubs;
using Microsoft.Extensions.Logging;
using Microsoft.Azure.WebJobs.Extensions.SignalRService;
using Newtonsoft.Json;

namespace Solution_Accelerator
{
    public static class Solution_Accelerator
    {
        private const string Signalr_Hub = "telemetryhub";
        private const string Consumer_Group = "telemetry-cg";

        [FunctionName("Telemetry_Processor")]
        public static async Task Run([EventHubTrigger("devicetelemetryhub", ConsumerGroup = "telemetry-functions-cg", Connection = "EVENTHUB_CS")] EventData[] eventData,
                                     [SignalR(HubName = Signalr_Hub)] IAsyncCollector<SignalRMessage> signalRMessage,
                                     ILogger log)
        {
            var exceptions = new List<Exception>();

            foreach (EventData ed in eventData)
            {
                try
                {
                    if (ed.SystemProperties.ContainsKey("iothub-message-source"))
                    {
                        string deviceId = ed.SystemProperties["iothub-connection-device-id"].ToString();
                        string msgSource = ed.SystemProperties["iothub-message-source"].ToString();
                        string signalr_target = string.Empty;
                        string model_id = string.Empty;

                        if (msgSource != "Telemetry")
                        {
                            log.LogInformation($"IoT Hub Message Source {msgSource}");
                        }

                        log.LogInformation($"Telemetry Source  : {msgSource}");
                        log.LogInformation($"Telemetry Message : {Encoding.UTF8.GetString(ed.Body.Array, ed.Body.Offset, ed.Body.Count)}");

                        DateTime enqueuTime = (DateTime)ed.SystemProperties["iothub-enqueuedtime"];

                        if (ed.SystemProperties.ContainsKey("dt-dataschema"))
                        {
                            model_id = ed.SystemProperties["dt-dataschema"].ToString();
                        }

                        NOTIFICATION_DATA signalrData = new NOTIFICATION_DATA
                        {
                            eventId = ed.SystemProperties["x-opt-sequence-number"].ToString(),
                            eventType = "Event Hubs",
                            eventTime = enqueuTime.ToUniversalTime().ToString("yyyy'-'MM'-'dd'T'HH':'mm':'ss'.'fff'Z'"),
                            eventSource = msgSource,
                            deviceId = deviceId,
                            dtDataSchema = model_id,
                            data = null
                        };

                        // Process telemetry based on message source
                        switch (msgSource)
                        {
                            case "Telemetry":
                                OnTelemetryReceived(signalrData, ed, log);
                                signalr_target = "DeviceTelemetry";
                                break;
                            case "twinChangeEvents":
                                OnDeviceTwinChanged(signalrData, ed, log);
                                signalr_target = "DeviceTwinChange";
                                break;
                            case "digitalTwinChangeEvents":
                                OnDigitalTwinTwinChanged(signalrData, ed, log);
                                signalr_target = "DigitalTwinChange";
                                break;
                            case "deviceLifecycleEvents":
                                OnDeviceLifecycleChanged(signalrData, ed, log);
                                signalr_target = "DeviceLifecycle";
                                break;
                            default:
                                break;
                        }

                        if (signalrData.data != null)
                        {
                            // send to SignalR Hub
                            var data = JsonConvert.SerializeObject(signalrData);

                            await signalRMessage.AddAsync(new SignalRMessage{
                                                                Target = signalr_target,
                                                                Arguments = new[] { data }
                                                            });
                        }

                        signalrData = null;
                    }
                    else
                    {
                        log.LogInformation("Unsupported Message Source");
                    }
                }
                catch (Exception e)
                {
                    exceptions.Add(e);
                }
            }

            if (exceptions.Count > 1)
                throw new AggregateException(exceptions);

            if (exceptions.Count == 1)
                throw exceptions.Single();
        }

        // Process Telemetry
        // Add filtering etc as needed
        // leave signalrData.data to null if we do not want to send SignalR message
        private static void OnTelemetryReceived(NOTIFICATION_DATA signalrData, EventData eventData, ILogger log)
        {
            log.LogInformation($"OnTelemetryReceived");
            signalrData.data = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
        }

        // Process Device Twin Change Event
        // Add filtering etc as needed
        // leave signalrData.data to null if we do not want to send SignalR message
        private static void OnDeviceTwinChanged(NOTIFICATION_DATA signalrData, EventData eventData, ILogger log)
        {
            log.LogInformation($"OnDeviceTwinChanged");
            signalrData.data = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
        }

        // Process Digital Twin Change Event
        // Add filtering etc as needed
        // leave signalrData.data to null if we do not want to send SignalR message
        private static void OnDigitalTwinTwinChanged(NOTIFICATION_DATA signalrData, EventData eventData, ILogger log)
        {
            log.LogInformation($"OnDigitalTwinTwinChanged");
            signalrData.data = Encoding.UTF8.GetString(eventData.Body.Array, eventData.Body.Offset, eventData.Body.Count);
        }

        // Process Device Lifecycle Change event
        // Add filtering etc as needed
        // leave signalrData.data to null if we do not want to send SignalR message
        private static void OnDeviceLifecycleChanged(NOTIFICATION_DATA signalrData, EventData eventData, ILogger log)
        {
            log.LogInformation($"OnDeviceLifecycleChanged");
            signalrData.data = JsonConvert.SerializeObject(eventData.Properties);
        }

        public class NOTIFICATION_DATA
        {
            public string eventId { get; set; }
            public string eventType { get; set; }
            public string deviceId { get; set; }
            public string eventSource { get; set; }
            public string eventTime { get; set; }
            public string data { get; set; }
            public string dtDataSchema { get; set; }
        }
    }
}
