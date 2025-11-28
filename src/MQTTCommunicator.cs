using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using BitRuisseau;
using MQTTnet;
using MQTTnet.Protocol;

namespace Backend;

public class MqttCommunicator 
{
    private const string DefaultTopic = "powercher";
    private readonly string _brokerIp;
    private IMqttClient _mqttClient;
    private readonly string _username;
    private readonly string _password;
    private readonly string _nodeId;
    private readonly string _topic;
    

    private readonly MqttClientFactory _factory = new();

    private bool _retain = false;
    private MqttQualityOfServiceLevel _qos = MqttQualityOfServiceLevel.AtLeastOnce;

    public MqttCommunicator(
        string brokerHost, string nodeId, string topic = MqttCommunicator.DefaultTopic,
        string username = "ict", string password = "321")
    {
        _nodeId = nodeId;
        _topic = topic;
        _brokerIp = GetPreferredIpAddress(brokerHost).ToString();        
        _username = username;
        _password = password;
        _mqttClient = _factory.CreateMqttClient();    }

    IPAddress GetPreferredIpAddress(string host)
    {
        //priority on the dgep ipv4 address
        return Dns.GetHostAddresses(host)
            .Where(/*V4*/address => address.AddressFamily == AddressFamily.InterNetwork)
            .Where(address => address.ToString().StartsWith("10"))
            .FirstOrDefault(Dns.GetHostAddresses(host)[0]);
    }

    public void Send(Message message, string? topic = null)
    {
        //For Senders only
        if (!_mqttClient.IsConnected)
        {
            Connect();
        }
        string json = JsonSerializer.Serialize(message);
        string payload = json;
        var applicationMessage = new MqttApplicationMessageBuilder()
            .WithTopic(topic ?? _topic)
            .WithRetainFlag(_retain)
            .WithQualityOfServiceLevel(_qos)
            .WithPayload(payload)
            .Build();

        //Async => sync
        var publishResult = _mqttClient.PublishAsync(applicationMessage).Result;
    }

    public Action<Message>? OnMessageReceived { private get; set; }
    public void Start()
    {
        //register context to be able to propagate exception to caller thread
        var syncContext = SynchronizationContext.Current;

        // Setup message handling before connecting so that queued messages
        // are also handled properly. When there is no event handler attached all
        // received messages get lost.
        _mqttClient.ApplicationMessageReceivedAsync += message =>
        {
            var payload = Encoding.UTF8.GetString(message.ApplicationMessage.Payload);
                try
                {
                    var msg = JsonSerializer.Deserialize<Message>(payload);
                    if (msg != null)
                    {
                        OnMessageReceived?.Invoke(msg);
                    }

                    return Task.CompletedTask;
                }
                catch (Exception e)
                {
                    // Post exception to main thread
                    // Commenting this stops invalid JSON from crashing this; keep observable errors for now
                    return Task.FromException(e);
                }

        };

        //Async => sync
        Connect();

        //Async => sync
        var mqttSubscribeOptions = _factory
            .CreateSubscribeOptionsBuilder()
            .WithTopicFilter(_topic,
                _qos,
                //noLocal:..,
                retainAsPublished: _retain,
                retainHandling: MqttRetainHandling.SendAtSubscribe)
            .Build();

        var subscriptionResult = _mqttClient.SubscribeAsync(mqttSubscribeOptions).Result;
        if (subscriptionResult.Items.Count() < 0)
        {
            throw new InvalidOperationException($"Failed to connect to the MQTT broker. Reason: {subscriptionResult.ReasonString}");
        }

    }

    private void Connect()
    {
        var options = new MqttClientOptionsBuilder()
            .WithClientId(_nodeId)
            .WithTcpServer(_brokerIp, 1883) //
            .WithCredentials(_username, _password)
            .Build();

        var connectResult = _mqttClient.ConnectAsync(options).Result;
        if (connectResult.ResultCode != MqttClientConnectResultCode.Success)
        {
            throw new InvalidOperationException($"Failed to connect to the MQTT broker. Reason: {connectResult.ReasonString}");
        }

    }

    public void Stop()
    {
        _mqttClient.DisconnectAsync();
    }
}