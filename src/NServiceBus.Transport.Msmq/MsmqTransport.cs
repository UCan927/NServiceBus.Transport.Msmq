namespace NServiceBus
{
    using System;
    using Features;
    using Routing;
    using Settings;
    using Transport;
    using Transport.Msmq;

    /// <summary>
    /// Transport definition for MSMQ.
    /// </summary>
    public class MsmqTransport : TransportDefinition, IMessageDrivenSubscriptionTransport
    {
        /// <summary>
        /// <see cref="TransportDefinition.ExampleConnectionStringForErrorMessage" />.
        /// </summary>
        public override string ExampleConnectionStringForErrorMessage => "cacheSendConnection=true;journal=false;deadLetter=true";

        /// <summary>
        /// <see cref="TransportDefinition.RequiresConnectionString" />.
        /// </summary>
        public override bool RequiresConnectionString => false;

        /// <summary>
        /// Initializes the transport infrastructure for msmq.
        /// </summary>
        /// <param name="settings">The settings.</param>
        /// <param name="connectionString">The connection string.</param>
        /// <returns>the transport infrastructure for msmq.</returns>
        public override TransportInfrastructure Initialize(SettingsHolder settings, string connectionString)
        {
            Guard.AgainstNull(nameof(settings), settings);

            if (!settings.GetOrDefault<bool>("Endpoint.SendOnly") && !settings.TryGetExplicitlyConfiguredErrorQueueAddress(out _))
            {
                throw new Exception("Faults forwarding requires an error queue to be specified using 'EndpointConfiguration.SendFailedMessagesTo()'");
            }

            if (connectionString != null)
            {
                var error = @"Passing in MSMQ settings such as DeadLetterQueue, Journaling etc via a connection string is no longer supported.  Use code level API. For example:
To turn off dead letter queuing, use: 
var transport = endpointConfiguration.UseTransport<MsmqTransport>();
transport.DisableDeadLetterQueueing();

To stop caching connections, use: 
var transport = endpointConfiguration.UseTransport<MsmqTransport>();
transport.DisableConnectionCachingForSends();

To use non-transactional queues, use:
var transport = endpointConfiguration.UseTransport<MsmqTransport>();
transport.UseNonTransactionalQueues();

To enable message journaling, use:
var transport = endpointConfiguration.UseTransport<MsmqTransport>();
transport.EnableJournaling();

To override the value of TTRQ, use:
var transport = endpointConfiguration.UseTransport<MsmqTransport>();
transport.TimeToReachQueue(timespanValue);";

                throw new Exception(error);
            }

            var msmqSettings = new MsmqSettings(settings);

            var isTransactional = IsTransactional(settings);
            var outBoxRunning = settings.IsFeatureActive(typeof(Features.Outbox));

            settings.TryGetAuditMessageExpiration(out var auditMessageExpiration);

            return new MsmqTransportInfrastructure(settings, msmqSettings, settings.Get<QueueBindings>(), isTransactional, outBoxRunning, auditMessageExpiration);
        }

        static bool IsTransactional(ReadOnlySettings settings)
        {
            //if user has asked for a explicit level infer IsTransactional from that setting
            if (settings.TryGet(out TransportTransactionMode requestedTransportTransactionMode))
            {
                return requestedTransportTransactionMode != TransportTransactionMode.None;
            }
            //otherwise use msmq default which is transactional
            return true;
        }
    }
}