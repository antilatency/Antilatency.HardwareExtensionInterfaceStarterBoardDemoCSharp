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
            TOP,
            BOTTOM,
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
            Thread.Sleep(100);
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
                Console.WriteLine("Side: TOP");
                return Sides.TOP;
            }
            if (state1 == PinState.High && state2 == PinState.Low)
            {
                Console.WriteLine("Side: BOTTOM");
                return Sides.BOTTOM;
            }
            Console.WriteLine("No Connection");
            return Sides.NoConnection;
        }

        static IOPins Config(Sides side)
        {
            var iOPins = new IOPins();
            switch (side)
            {
                case Sides.TOP:
                    iOPins.STATUS1 = Pins.IO6;
                    iOPins.STATUS2 = Pins.IO1;
                    iOPins.FUNC1 = Pins.IO5;
                    iOPins.FUNC2 = Pins.IO2;
                    iOPins.H_AXIS = Pins.IOA4;
                    iOPins.V_AXIS = Pins.IOA3;
                    iOPins.CLICK = Pins.IO7;
                    break;
                case Sides.BOTTOM:
                    iOPins.STATUS1 = Pins.IO1;
                    iOPins.STATUS2 = Pins.IO6;
                    iOPins.FUNC1 = Pins.IO2;
                    iOPins.FUNC2 = Pins.IO5;
                    iOPins.H_AXIS = Pins.IOA3;
                    iOPins.V_AXIS = Pins.IOA4;
                    iOPins.CLICK = Pins.IO8;
                    break;
                case Sides.NoConnection: break;
                case Sides.ShortCircuit: break;

            }
            return iOPins;
        }
           
        static void Run (Antilatency.HardwareExtensionInterface.ICotask cotask, IOPins conf) {
            using var ledRed = cotask.createPwmPin(conf.STATUS1, 1000, 0f);
            using var ledGreen = cotask.createOutputPin(conf.STATUS2, PinState.High);
            using var Haxis = cotask.createAnalogPin(conf.H_AXIS, 10);
            using var Vaxis = cotask.createAnalogPin(conf.V_AXIS, 10);
            using var FUNC1 = cotask.createInputPin(conf.FUNC1);
            using var FUNC2 = cotask.createInputPin(conf.FUNC2);
            using var CLICK = cotask.createInputPin(conf.CLICK);
         
            
            cotask.run();
            
            while (!cotask.isTaskFinished()) {
                Console.WriteLine($"HAxis: {Math.Round(Haxis.getValue(),2), -5} VAxis {Math.Round(Vaxis.getValue(), 2), -5} FUNC1: {FUNC1.getState(), -5} FUNC2: {FUNC2.getState(),-5} CLICK: {CLICK.getState(),-5}");

                ledRed.setDuty(Haxis.getValue() * 0.4f);


                if (Vaxis.getValue() >= 2) {
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