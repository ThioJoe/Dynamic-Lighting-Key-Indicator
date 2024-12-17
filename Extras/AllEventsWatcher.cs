using Microsoft.UI.Xaml;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Linq.Expressions;
using System.Reflection;

namespace Dynamic_Lighting_Key_Indicator.Utils
{
    // Used to print to console any time any event is triggered on a target object. Useful for testing purposes.
    public class AllEventsWatcher
    {
        private Dictionary<string, Delegate> EventHandlersHolder = [];
        private static List<AllEventsWatcher> Instances = [];  // Keep track of instances of this class so the user can see how many are watching
        private object? Target { get; init; }
        private string? TargetTypeName { get; init; }

        // Events that are noisy and may be chosen to be excluded automatically
        private readonly List<string> NoisyEventNames = ["LayoutUpdated", "PointerMoved", "PointerEntered", "PointerExited"];

        /// <summary>
        /// Initializes a new instance of the <see cref="AllEventsWatcher"/> class and starts watching events on the target object.
        /// </summary>
        /// <param name="target">The target object to watch events on.</param>
        /// <param name="excludedEventNames">A list of event names to exclude from watching.</param>
        /// <param name="excludeKnownNoisyEvents">Whether to exclude known noisy events.</param>
        public AllEventsWatcher(object target, List<string>? excludedEventNames = null, bool excludeKnownNoisyEvents = false)
        {
            List<string> final_excludedEventNames = [];

            if (excludeKnownNoisyEvents)
                final_excludedEventNames = NoisyEventNames;

            if (excludedEventNames != null)
                final_excludedEventNames.AddRange(excludedEventNames);

            Target = target;
            TargetTypeName = target.GetType().Name;
            StartWatchingEvents(target, final_excludedEventNames);
        }

        // --------------------- Static Methods ---------------------

        /// <summary>
        /// Creates a new instance of <see cref="AllEventsWatcher"/> and starts watching events on the target object.
        /// </summary>
        /// <param name="target">The target object to watch events on.</param>
        /// <param name="excludedEventNames">A list of event names to exclude from watching.</param>
        /// <param name="excludeKnownNoisyEvents">Whether to exclude known noisy events.</param>
        /// <param name="stopIfDuplicate">Whether to stop if a duplicate watcher is found.</param>
        /// <param name="stopIfDuplicate">If a watcher instance is already found running of the target, whether to stop watching it. Effectively toggles watching of the target.</param>
        public static AllEventsWatcher? Create(object target, List<string>? excludedEventNames = null, bool excludeKnownNoisyEvents = false, bool stopIfDuplicate = false)
        {
            if (stopIfDuplicate)
            {
                AllEventsWatcher? instance = FindInstance_ByTarget(target);
                if (instance != null)
                {
                    instance.Stop();
                    return null;
                }
            }

            // Create a new instance if not stopped or not found
            return new AllEventsWatcher(target, excludedEventNames, excludeKnownNoisyEvents);
        }

        /// <summary>
        /// Creates a new instance of <see cref="AllEventsWatcher"/> by finding a control by name on the specified window and starts watching events on it.
        /// </summary>
        /// <param name="parentWindow">The parent window containing the control.</param>
        /// <param name="controlName">The name of the control to watch events on.</param>
        /// <param name="excludedEventNames">A list of event names to exclude from watching.</param>
        /// <param name="excludeKnownNoisyEvents">Whether to exclude known noisy events.</param>
        /// <param name="stopIfDuplicate">If a watcher instance is already found running of the target, whether to stop watching it. Effectively toggles watching of the target.</param>
        /// <returns>A new instance of <see cref="AllEventsWatcher"/> if no duplicate is found; otherwise, null.</returns>
        public static AllEventsWatcher? Create_ByControlNameOnWindow(Microsoft.UI.Xaml.Window parentWindow, string controlName, List<string>? excludedEventNames = null, bool excludeKnownNoisyEvents = false, bool stopIfDuplicate = false)
        {
            object? control = FindWindowControlByName(parentWindow, controlName);
            if (control != null)
            {
                AllEventsWatcher? instance = FindInstance_ByTarget(control);
                if (instance == null)
                {
                    return new AllEventsWatcher(control, excludedEventNames, excludeKnownNoisyEvents);
                }
                else if (stopIfDuplicate)
                {
                    instance.Stop();
                    return null;
                }
                else
                {
                    return instance;
                }
            }
            return null;
        }

        /// <summary>
        /// Gets the count of active <see cref="AllEventsWatcher"/> instances.
        /// </summary>
        /// <returns>The count of active instances.</returns>
        public static int GetInstancesCount()
        {
            return Instances.Count;
        }

        /// <summary>
        /// Stops all active <see cref="AllEventsWatcher"/> instances.
        /// </summary>
        public static void StopAllWatcherInstances()
        {
            foreach (var instance in Instances)
            {
                instance.Stop();
            }
        }

        /// <summary>
        /// Finds an active instance of <see cref="AllEventsWatcher"/> by the target object.
        /// </summary>
        /// <param name="target">The target object to find the watcher for.</param>
        /// <returns>The instance of <see cref="AllEventsWatcher"/> if found; otherwise, null.</returns>
        public static AllEventsWatcher? FindInstance_ByTarget(object target)
        {
            return Instances.Find(i => i.Target == target);
        }

        /// <summary>
        /// Finds active instances of <see cref="AllEventsWatcher"/> by the target type name.
        /// </summary>
        /// <param name="targetTypeName">The name of the target type to find watchers for.</param>
        /// <returns>A list of instances of <see cref="AllEventsWatcher"/> found.</returns>
        public static List<AllEventsWatcher> FindInstance_ByTargetTypeName(string targetTypeName)
        {
            List<AllEventsWatcher> foundInstances = Instances.FindAll(i => i.TargetTypeName == targetTypeName);
            return foundInstances;
        }

        /// <summary>
        /// Finds a control by name on the specified window.
        /// </summary>
        /// <param name="parentWindow">The parent window containing the control.</param>
        /// <param name="name">The name of the control to find.</param>
        /// <returns>The control if found; otherwise, null.</returns>
        private static object? FindWindowControlByName(Microsoft.UI.Xaml.Window parentWindow, string name)
        {
            if (parentWindow.Content is FrameworkElement windowRoot && windowRoot.XamlRoot != null)
            {
                object? control = windowRoot.FindName(name);
                return control;
            }
            return null;
        }

        // --------------------- Instance Methods ---------------------

        /// <summary>
        /// Gets the count of events being watched.
        /// </summary>
        /// <returns>The count of events being watched.</returns>
        public int GetEventsWatchedCount()
        {
            return EventHandlersHolder.Count;
        }

        /// <summary>
        /// Lists the names of the events being watched.
        /// </summary>
        /// <returns>A list of event names being watched on the instance target.</returns>
        public List<string> ListWatchedEvents()
        {
            return EventHandlersHolder.Keys.ToList();
        }

        /// <summary>
        /// Starts watching events on the target object.
        /// </summary>
        /// <param name="target">The target object to watch events on.</param>
        /// <param name="excludedEventNames">A list of event names to exclude from watching.</param>
        private void StartWatchingEvents(object target, List<string> excludedEventNames)
        {
            ArgumentNullException.ThrowIfNull(target);

            // Use reflection to get all events of advancedInfoStack
            EventInfo[] events = target.GetType().GetEvents();

            foreach (var eventInfo in events)
            {
                Type? handlerType = eventInfo.EventHandlerType;

                if (handlerType == null)
                    continue;

                if (excludedEventNames.Contains(eventInfo.Name))
                    continue;

                try
                {
                    // Create delegate dynamically
                    Delegate? handler = CreateEventHandler(handlerType, eventInfo.Name);

                    if (handler != null)
                    {
                        eventInfo.AddEventHandler(target, handler);
                        // Store the handler for later removal
                        EventHandlersHolder[eventInfo.Name] = handler;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error attaching to event {eventInfo.Name}: {ex.Message}");
                }
            }

            if (EventHandlersHolder.Count == 0)
            {
                Debug.WriteLine("No events found to watch");
            }
            else
            {
                Debug.WriteLine($" ---------- {TargetTypeName} - Started Watching {EventHandlersHolder.Count} events -----------");
                Instances.Add(this);
            }
        }

        /// <summary>
        /// Stops watching events on the target object.
        /// </summary>
        public void Stop()
        {
            int initialCount = EventHandlersHolder.Count;

            if (initialCount == 0)
            {
                Debug.WriteLine("No events were being watched.");
                return;
            }

            object? target = Target;
            ArgumentNullException.ThrowIfNull(target);

            Type targetType = target.GetType();

            foreach (KeyValuePair<string, Delegate> kvp in EventHandlersHolder)
            {
                try
                {
                    string name = kvp.Key;
                    Delegate handler = kvp.Value;

                    // Remove the handler
                    EventInfo? eventInfo = targetType.GetEvent(name);
                    eventInfo?.RemoveEventHandler(target, handler);
                    // Remove from dictionary
                    EventHandlersHolder.Remove(name);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Error detaching from event {kvp.Key}: {ex.Message}");
                }
            }

            int failedCount = EventHandlersHolder.Count;
            if (EventHandlersHolder.Count != 0)
            {
                Debug.WriteLine($"Failed to detach from {failedCount} of {initialCount} event handlers:");
                foreach (var kvp in EventHandlersHolder)
                {
                    Debug.WriteLine($"   Possible Remaining Event Name: {kvp.Key}");
                }
            }
            else
            {
                Instances.Remove(this);
                Debug.WriteLine($" ------------------- Stopped Watching {initialCount} events -------------------");
            }

        }

        /// <summary>
        /// Creates an event handler delegate dynamically.
        /// </summary>
        /// <param name="handlerType">The type of the event handler delegate.</param>
        /// <param name="eventName">The name of the event.</param>
        /// <returns>The created event handler delegate.</returns>
        private Delegate? CreateEventHandler(Type handlerType, string eventName)
        {
            // Get the Invoke method of the delegate
            MethodInfo? invokeMethod = handlerType.GetMethod("Invoke");
            ParameterInfo[]? parameters = invokeMethod?.GetParameters();

            // Create parameters expressions
            ParameterExpression[]? parameterExpressions = parameters?.Select(p => Expression.Parameter(p.ParameterType, p.Name)).ToArray();

            // Create expression to call Debug.WriteLine($"Event: {eventName}")
            ConstantExpression eventNameExpression = Expression.Constant($"Event: {eventName}");
            MethodInfo? debugWriteLineMethod = typeof(Debug).GetMethod(nameof(Debug.WriteLine), new Type[] { typeof(string) });

            if (debugWriteLineMethod == null)
            {
                Debug.WriteLine("Debug.WriteLine method not found");
                return null;
            }

            MethodCallExpression callDebugWriteLine = Expression.Call(debugWriteLineMethod, eventNameExpression);

            // Create lambda expression
            var handler = Expression.Lambda(handlerType, callDebugWriteLine, parameterExpressions);

            return handler.Compile();
        }
    }

}
