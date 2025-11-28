using System;
using System.Threading;
using Apache.NMS;
using Apache.NMS.ActiveMQ;

namespace NotificationService
{
    /// <summary>
    /// Notification service that publishes bike-sharing system events to ActiveMQ.
    /// Generates simulated events for pollution alerts, station maintenance, availability changes, weather warnings, and traffic alerts.
    /// </summary>
    internal class Program
    {
        private static bool _isRunning = true;

        static void Main(string[] args)
        {
            Console.WriteLine("╔══════════════════════════════════════════╗");
            Console.WriteLine("║           Notification Service           ║");
            Console.WriteLine("╚══════════════════════════════════════════╝");
            Console.WriteLine();

            Console.CancelKeyPress += (sender, e) =>
            {
                Console.WriteLine("\n[NotificationService] Shutting down...");
                _isRunning = false;
                e.Cancel = true;
            };

            try
            {
                RunNotificationService();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[NotificationService] FATAL ERROR: {ex.Message}");
                Console.WriteLine($"[NotificationService] Stack trace: {ex.StackTrace}");
            }
            finally
            {
                Console.WriteLine("\n[NotificationService] Service stopped");
            }
        }

        /// <summary>
        /// Main service loop that publishes notifications to ActiveMQ topic.
        /// </summary>
        static void RunNotificationService()
        {
            IConnectionFactory factory = null;
            IConnection connection = null;
            ISession session = null;
            IMessageProducer producer = null;

            try
            {
                Console.WriteLine("[NotificationService] Connecting to ActiveMQ (tcp://localhost:61616)...");
                factory = new ConnectionFactory("tcp://localhost:61616");
                connection = factory.CreateConnection();
                connection.Start();
                Console.WriteLine("[NotificationService] Connected to ActiveMQ");

                session = connection.CreateSession(AcknowledgementMode.AutoAcknowledge);

                IDestination topic = session.GetTopic("notifications.global");
                Console.WriteLine("[NotificationService] Topic 'notifications.global' created");

                producer = session.CreateProducer(topic);
                producer.DeliveryMode = MsgDeliveryMode.NonPersistent;
                producer.TimeToLive = TimeSpan.FromMinutes(5);

                Console.WriteLine("\n[NotificationService] Publishing notifications...");
                Console.WriteLine("[NotificationService] Press Ctrl+C to stop\n");

                Random rand = new Random();
                int messageCount = 0;

                while (_isRunning)
                {
                    try
                    {
                        string eventType = GetRandomEventType(rand);
                        string msgText = GenerateNotificationMessage(eventType, rand);

                        ITextMessage message = producer.CreateTextMessage(msgText);

                        message.Properties["EventType"] = eventType;
                        message.Properties["Timestamp"] = DateTime.UtcNow.ToString("o");
                        message.Properties["Severity"] = GetSeverity(eventType);

                        producer.Send(message);
                        messageCount++;

                        Console.WriteLine($"[{DateTime.Now:HH:mm:ss}] [{messageCount}] Published: {msgText}");

                        Thread.Sleep(GetDelayForEventType(eventType));
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"[NotificationService] Send error: {ex.Message}");
                        Thread.Sleep(1000);
                    }
                }
            }
            catch (NMSConnectionException ex)
            {
                Console.WriteLine($"\n[NotificationService] Connection error:");
                Console.WriteLine($"[NotificationService] {ex.Message}");
                Console.WriteLine($"\n[NotificationService] Please ensure ActiveMQ is running on tcp://localhost:61616");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"\n[NotificationService] Error: {ex.Message}");
            }
            finally
            {
                producer?.Close();
                session?.Close();
                connection?.Close();
                Console.WriteLine("[NotificationService] Disconnected from ActiveMQ");
            }
        }

        /// <summary>
        /// Generates a random event type for notification simulation.
        /// </summary>
        /// <param name="rand">Random number generator</param>
        /// <returns>Event type identifier</returns>
        static string GetRandomEventType(Random rand)
        {
            string[] eventTypes =
            {
                "POLLUTION_ALERT",
                "STATION_MAINTENANCE",
                "BIKE_AVAILABILITY_LOW",
                "BIKE_AVAILABILITY_HIGH",
                "STATION_FULL",
                "WEATHER_WARNING",
                "TRAFFIC_ALERT"
            };

            return eventTypes[rand.Next(eventTypes.Length)];
        }

        /// <summary>
        /// Generates a realistic notification message based on event type.
        /// </summary>
        /// <param name="eventType">Type of event</param>
        /// <param name="rand">Random number generator</param>
        /// <returns>Formatted notification message</returns>
        static string GenerateNotificationMessage(string eventType, Random rand)
        {
            string[] frenchCities = { "Lyon", "Paris", "Nantes", "Toulouse", "Nancy", "Besançon", "Amiens" };
            string city = frenchCities[rand.Next(frenchCities.Length)];

            switch (eventType)
            {
                case "POLLUTION_ALERT":
                    int pollutionLevel = rand.Next(80, 180);
                    return $"POLLUTION ALERT: {city} - Level {pollutionLevel} μg/m³ (Threshold: 80)";

                case "STATION_MAINTENANCE":
                    return $"MAINTENANCE: Station '{GetRandomStationName(rand)}' in {city} temporarily closed";

                case "BIKE_AVAILABILITY_LOW":
                    int bikesLeft = rand.Next(0, 3);
                    return $"LOW BIKE AVAILABILITY: {city} - {bikesLeft} bike(s) available at {GetRandomStationName(rand)}";

                case "BIKE_AVAILABILITY_HIGH":
                    return $"BIKES AVAILABLE: {city} - Station '{GetRandomStationName(rand)}' restocked";

                case "STATION_FULL":
                    return $"STATION FULL: {city} - '{GetRandomStationName(rand)}' - No parking spaces available";

                case "WEATHER_WARNING":
                    string[] warnings = { "Heavy rain", "Strong wind", "Snow", "Thunderstorm" };
                    return $"WEATHER WARNING: {city} - {warnings[rand.Next(warnings.Length)]} - Be careful!";

                case "TRAFFIC_ALERT":
                    return $"TRAFFIC ALERT: {city} - Heavy congestion, consider using bikes";

                default:
                    return $"NOTIFICATION: Event in {city}";
            }
        }

        /// <summary>
        /// Generates a random station name for notification simulation.
        /// </summary>
        /// <param name="rand">Random number generator</param>
        /// <returns>Station name</returns>
        static string GetRandomStationName(Random rand)
        {
            string[] stations =
            {
                "Gare Part-Dieu", "Bellecour", "Hôtel de Ville", "République",
                "Place Carnot", "Perrache", "Saxe-Gambetta", "Vieux Lyon"
            };
            return stations[rand.Next(stations.Length)];
        }

        /// <summary>
        /// Determines the severity level for a given event type.
        /// </summary>
        /// <param name="eventType">Type of event</param>
        /// <returns>Severity level (HIGH, MEDIUM, or LOW)</returns>
        static string GetSeverity(string eventType)
        {
            switch (eventType)
            {
                case "POLLUTION_ALERT":
                case "WEATHER_WARNING":
                    return "HIGH";

                case "STATION_MAINTENANCE":
                case "STATION_FULL":
                case "BIKE_AVAILABILITY_LOW":
                    return "MEDIUM";

                default:
                    return "LOW";
            }
        }

        /// <summary>
        /// Determines the delay between messages based on event type.
        /// </summary>
        /// <param name="eventType">Type of event</param>
        /// <returns>Delay in milliseconds</returns>
        static int GetDelayForEventType(string eventType)
        {
            switch (eventType)
            {
                case "POLLUTION_ALERT":
                case "WEATHER_WARNING":
                    return 10000; // 10 seconds (less frequent)

                case "BIKE_AVAILABILITY_LOW":
                case "BIKE_AVAILABILITY_HIGH":
                    return 3000; // 3 seconds (frequent)

                default:
                    return 5000; // 5 seconds (normal)
            }
        }
    }
}
