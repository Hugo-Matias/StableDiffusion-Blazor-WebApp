using BlazorWebApp.Events;
using BlazorWebApp.Services;
using FluentAssertions;
using Xunit;

namespace BlazorWebApp.Tests.Services
{
    /// <summary>
    /// Unit tests for EventService typed event aggregation
    /// </summary>
    public class EventServiceTests
    {
        private readonly EventService _sut;

        public EventServiceTests()
        {
            _sut = new EventService();
        }

        [Fact]
        public void Subscribe_ShouldIncreaseSubscriberCount()
        {
            // Arrange
            Action<StateChangedEventArgs> handler = _ => { };

            // Act
            _sut.Subscribe(handler);

            // Assert
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(1);
        }

        [Fact]
        public void Subscribe_WithMultipleHandlers_ShouldIncreaseSubscriberCount()
        {
            // Arrange
            Action<StateChangedEventArgs> handler1 = _ => { };
            Action<StateChangedEventArgs> handler2 = _ => { };

            // Act
            _sut.Subscribe(handler1);
            _sut.Subscribe(handler2);

            // Assert
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(2);
        }

        [Fact]
        public void Subscribe_WithNullHandler_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => _sut.Subscribe<StateChangedEventArgs>(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("handler");
        }

        [Fact]
        public void Unsubscribe_ShouldDecreaseSubscriberCount()
        {
            // Arrange
            Action<StateChangedEventArgs> handler = _ => { };
            _sut.Subscribe(handler);

            // Act
            _sut.Unsubscribe(handler);

            // Assert
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(0);
        }

        [Fact]
        public void Unsubscribe_WithNullHandler_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => _sut.Unsubscribe<StateChangedEventArgs>(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("handler");
        }

        [Fact]
        public void Unsubscribe_NonExistentHandler_ShouldNotThrow()
        {
            // Arrange
            Action<StateChangedEventArgs> handler = _ => { };

            // Act
            Action act = () => _sut.Unsubscribe(handler);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Publish_ShouldInvokeSubscribedHandlers()
        {
            // Arrange
            var invoked = false;
            Action<StateChangedEventArgs> handler = _ => invoked = true;
            _sut.Subscribe(handler);
            var eventArgs = new StateChangedEventArgs { ChangeType = StateChangeType.AppState };

            // Act
            _sut.Publish(eventArgs);

            // Assert
            invoked.Should().BeTrue();
        }

        [Fact]
        public void Publish_ShouldInvokeAllSubscribedHandlers()
        {
            // Arrange
            var invocationCount = 0;
            Action<StateChangedEventArgs> handler1 = _ => invocationCount++;
            Action<StateChangedEventArgs> handler2 = _ => invocationCount++;
            Action<StateChangedEventArgs> handler3 = _ => invocationCount++;

            _sut.Subscribe(handler1);
            _sut.Subscribe(handler2);
            _sut.Subscribe(handler3);

            var eventArgs = new StateChangedEventArgs { ChangeType = StateChangeType.AppState };

            // Act
            _sut.Publish(eventArgs);

            // Assert
            invocationCount.Should().Be(3);
        }

        [Fact]
        public void Publish_WithNullEventArgs_ShouldThrowArgumentNullException()
        {
            // Act
            Action act = () => _sut.Publish<StateChangedEventArgs>(null!);

            // Assert
            act.Should().Throw<ArgumentNullException>()
                .WithParameterName("eventArgs");
        }

        [Fact]
        public void Publish_WithNoSubscribers_ShouldNotThrow()
        {
            // Arrange
            var eventArgs = new StateChangedEventArgs { ChangeType = StateChangeType.AppState };

            // Act
            Action act = () => _sut.Publish(eventArgs);

            // Assert
            act.Should().NotThrow();
        }

        [Fact]
        public void Publish_ShouldPassCorrectEventArgsToHandlers()
        {
            // Arrange
            StateChangedEventArgs? receivedEventArgs = null;
            Action<StateChangedEventArgs> handler = e => receivedEventArgs = e;
            _sut.Subscribe(handler);

            var expectedEventArgs = new StateChangedEventArgs
            {
                ChangeType = StateChangeType.GenerationParameters,
                OldValue = "old",
                NewValue = "new"
            };

            // Act
            _sut.Publish(expectedEventArgs);

            // Assert
            receivedEventArgs.Should().NotBeNull();
            receivedEventArgs!.ChangeType.Should().Be(StateChangeType.GenerationParameters);
            receivedEventArgs.OldValue.Should().Be("old");
            receivedEventArgs.NewValue.Should().Be("new");
        }

        [Fact]
        public void Publish_WhenHandlerThrows_ShouldContinueInvokingOtherHandlers()
        {
            // Arrange
            var handler1Invoked = false;
            var handler2Invoked = false;
            var handler3Invoked = false;

            Action<StateChangedEventArgs> handler1 = _ => handler1Invoked = true;
            Action<StateChangedEventArgs> handler2 = _ => throw new InvalidOperationException("Test exception");
            Action<StateChangedEventArgs> handler3 = _ => handler3Invoked = true;

            _sut.Subscribe(handler1);
            _sut.Subscribe(handler2);
            _sut.Subscribe(handler3);

            var eventArgs = new StateChangedEventArgs { ChangeType = StateChangeType.AppState };

            // Act
            _sut.Publish(eventArgs);

            // Assert
            handler1Invoked.Should().BeTrue();
            handler3Invoked.Should().BeTrue();
        }

        [Fact]
        public void Publish_WithDifferentEventTypes_ShouldOnlyInvokeMatchingHandlers()
        {
            // Arrange
            var stateHandlerInvoked = false;
            var modelHandlerInvoked = false;

            Action<StateChangedEventArgs> stateHandler = _ => stateHandlerInvoked = true;
            Action<ModelChangedEventArgs> modelHandler = _ => modelHandlerInvoked = true;

            _sut.Subscribe(stateHandler);
            _sut.Subscribe(modelHandler);

            var stateEventArgs = new StateChangedEventArgs { ChangeType = StateChangeType.AppState };

            // Act
            _sut.Publish(stateEventArgs);

            // Assert
            stateHandlerInvoked.Should().BeTrue();
            modelHandlerInvoked.Should().BeFalse();
        }

        [Fact]
        public void GetSubscriberCount_WithNoSubscribers_ShouldReturnZero()
        {
            // Act
            var count = _sut.GetSubscriberCount<StateChangedEventArgs>();

            // Assert
            count.Should().Be(0);
        }

        [Fact]
        public void SubscribeAndUnsubscribe_MultipleTimes_ShouldMaintainCorrectCount()
        {
            // Arrange
            Action<StateChangedEventArgs> handler1 = _ => { };
            Action<StateChangedEventArgs> handler2 = _ => { };

            // Act & Assert
            _sut.Subscribe(handler1);
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(1);

            _sut.Subscribe(handler2);
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(2);

            _sut.Unsubscribe(handler1);
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(1);

            _sut.Unsubscribe(handler2);
            _sut.GetSubscriberCount<StateChangedEventArgs>().Should().Be(0);
        }
    }
}
