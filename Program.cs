using System;
using System.Collections.Generic;
using System.Drawing;
using System.Net.Http.Headers;
using System.Runtime.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Antilatency.DeviceNetwork;
using Antilatency.HardwareExtensionInterface;
using Antilatency.HardwareExtensionInterface.Interop;

namespace AheiDemo {
    class Program {
        public enum Sides
        {
            Top,
            Bottom,
            NoConnection,
            ShortCircuit

        }

        struct IOPins
        {
            public Pins H_AXIS;
            public Pins V_AXIS;
            public Pins STATUS1;
            public Pins STATUS2;
            public Pins FUNC1;
            public Pins FUNC2;
            public Pins CLICK;

        }
        static Sides SideCheck(Antilatency.HardwareExtensionInterface.ICotask cotask)
        {
            using var button1 = cotask.createInputPin(Pins.IO1);
            using var button2 = cotask.createInputPin(Pins.IO6);
           
            cotask.run();
            Thread.Sleep(10);
            var state1 = button1.getState();
            var state2 = button2.getState();
            Console.WriteLine($"{state1} {state2}");

            if (state1 == PinState.Low && state2 == PinState.Low)
            {
                Console.WriteLine("ShortCircuit");
                return Sides.ShortCircuit;
            }
            if (state1 == PinState.Low && state2 == PinState.High)
            {
                Console.WriteLine("Side: Top");
                return Sides.Top;
            }
            if (state1 == PinState.High && state2 == PinState.Low)
            {
                Console.WriteLine("Side: Bottom");
                return Sides.Bottom;
            }
            Console.WriteLine("No Connection");
            return Sides.NoConnection;
        }

        static IOPins Config(Sides side)
        {
            var iOPins = new IOPins();
            switch (side)
            {
                case Sides.Top:
                    iOPins.STATUS1 = Pins.IO6;
                    iOPins.STATUS2 = Pins.IO1;
                    iOPins.FUNC1 = Pins.IO5;
                    iOPins.FUNC2 = Pins.IO2;
                    iOPins.H_AXIS = Pins.IOA4;
                    iOPins.V_AXIS = Pins.IOA3;
                    iOPins.CLICK = Pins.IO7;
                    break;
                case Sides.Bottom:
                    iOPins.STATUS1 = Pins.IO1;
                    iOPins.STATUS2 = Pins.IO6;
                    iOPins.FUNC1 = Pins.IO2;
                    iOPins.FUNC2 = Pins.IO5;
                    iOPins.H_AXIS = Pins.IOA3;
                    iOPins.V_AXIS = Pins.IOA4;
                    iOPins.CLICK = Pins.IO8;
                    break;
                case Sides.NoConnection:
                    throw new InvalidOperationException("Such connection is not supported.");
                case Sides.ShortCircuit:
                    throw new InvalidOperationException("Such connection is not supported.");

            }
            return iOPins;
        }
           
        static void Run (Antilatency.HardwareExtensionInterface.ICotask cotask, IOPins conf) {
            using var ledRed = cotask.createPwmPin(conf.STATUS1, 1000, 0f);
            using var ledGreen = cotask.createOutputPin(conf.STATUS2, PinState.High);
            using var hAxis = cotask.createAnalogPin(conf.H_AXIS, 10);
            using var vAxis = cotask.createAnalogPin(conf.V_AXIS, 10);
            using var func1 = cotask.createInputPin(conf.FUNC1);
            using var func2 = cotask.createInputPin(conf.FUNC2);
            using var click = cotask.createInputPin(conf.CLICK);
         
            
            cotask.run();
            
            while (!cotask.isTaskFinished()) {
                Console.WriteLine("hAxis: {0,-5} vAxis {1,-5} func1: {2,-5} func2: {3,-5} click: {4,-5}",
                   Math.Round(hAxis.getValue(), 2),
                   Math.Round(vAxis.getValue(), 2),
                   func1.getState(),
                   func2.getState(),
                   click.getState());

                ledRed.setDuty(hAxis.getValue() * 0.4f);


                if (vAxis.getValue() >= 2) {
                    ledGreen.setState(PinState.High);
                        }
                else ledGreen.setState(PinState.Low);


                Thread.Sleep(10);
                Console.CursorLeft = 0;
                Console.CursorTop = 1;
            }
            cotask.Dispose();
            
        }


        static void Main(string[] args) {
            // Load the Antilatency Device Network library
            using var deviceNetworkLibrary = Antilatency.DeviceNetwork.Library.load();
            Console.WriteLine($"Antilatency Device Network version: {deviceNetworkLibrary.getVersion()}");

            // Create a device network filter and then create a network using that filter.
            var networkFilter = deviceNetworkLibrary.createFilter();
            networkFilter.addUsbDevice(Antilatency.DeviceNetwork.Constants.AllUsbDevices);
            using var network = deviceNetworkLibrary.createNetwork(networkFilter);

            using var aheiLib = Antilatency.HardwareExtensionInterface.Library.load();
            Console.WriteLine($"Ahei version: {aheiLib.getVersion()}");

            using var cotaskConstructor = aheiLib.getCotaskConstructor();

            var targetNode = Antilatency.DeviceNetwork.NodeHandle.Null;

            while (targetNode == Antilatency.DeviceNetwork.NodeHandle.Null)
            {
                var supportedNodes = cotaskConstructor.findSupportedNodes(network);
                foreach (var node in supportedNodes)
                {
                    if (network.nodeGetStringProperty(node, "Tag") == "ExBoard")
                    {
                        targetNode = node;
                        break;
                    }
                }
                Console.WriteLine($"Nodes count {supportedNodes.Length}");
                Thread.Sleep(100);
            }

            IOPins conf;

            while (true)
            {
                var cotask = cotaskConstructor.startTask(network, targetNode);

                var side = SideCheck(cotask);
                conf = Config(side);
                cotask.Dispose();
                Console.Clear();
                while (side == Sides.NoConnection || side == Sides.ShortCircuit)
                {
                    Console.WriteLine("Wrong connection");
                    cotask = cotaskConstructor.startTask(network, targetNode);
                    side = SideCheck(cotask);

                    cotask.Dispose();
                    Thread.Sleep(100);
                    Console.Clear();
                }
                cotask = cotaskConstructor.startTask(network, targetNode);
                Console.WriteLine("Side:" + side);
                Run(cotask, conf);
                Console.Clear();
                targetNode = Antilatency.DeviceNetwork.NodeHandle.Null;

                while (targetNode == Antilatency.DeviceNetwork.NodeHandle.Null)
                {
                    var supportedNodes = cotaskConstructor.findSupportedNodes(network);
                    foreach (var node in supportedNodes)
                    {
                        if (network.nodeGetStringProperty(node, "Tag") == "ExBoard")
                        {
                            targetNode = node;
                            break;
                        }
                    }
                    Console.WriteLine($"Invalid Note! Connect the proper device.");
                    Thread.Sleep(100);

                    Console.CursorLeft = 0;
                    Console.CursorTop = 0;
                }

            }
        }
    }
}