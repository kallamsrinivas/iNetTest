
using ISC.iNet.DS.DomainModel;
using ISC.iNet.DS.Instruments;
using ISC.iNet.DS.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using Moq;
using System;
using System.Collections.Generic;
using Xunit;
using static ISC.iNet.DS.Instruments.InstrumentController;

namespace ISC.iNet.DS.UnitTests
{

    public class BumpInstruments : IDisposable
    {
        Master master = null;

        public BumpInstruments()
        {
            master = Master.CreateMaster();
            Mock<ISwitchService> switchService = new Mock<ISwitchService>();
            Mock<IConsoleService> consoleService = new Mock<IConsoleService>();
            Mock<InstrumentController> instrumentController = new Mock<InstrumentController>();
            //instrumentController.CallBase = true;
            // Mock the method
            instrumentController.Setup(c => c.Initialize(It.IsAny<Mode>()));

            Mock<ControllerWrapper> controller = new Mock<ControllerWrapper>();
            controller.Setup(c => c.IsDocked()).Returns(true);
            controller.Setup(c => c.IsPumpAdapterAttached()).Returns(false);

            Instrument instrument = new Instrument();
            instrument.Type = DeviceType.MX6;
            switchService.Setup(foo => foo.InstrumentController).Returns(instrumentController.Object);
            switchService.Setup(foo => foo.Instrument).Returns(instrument);
            master.SwitchService = switchService.Object;
            master.ControllerWrapper = controller.Object;
           
        }


        [Fact(Skip= "Sample test, not for execution")]
        public void ShouldThrowNotSupportedException()
        {
            //Arrange
            InstrumentBumpTestAction action = new InstrumentBumpTestAction();
            action.DockingStation = new DockingStation();
            action.Instrument = new Instrument();

            Configuration.DockingStation = action.DockingStation;

            //Act and Assert
            Xunit.Assert.Throws<NotSupportedException>(() => new InstrumentBumpTestOperation(action));
        }

        [Fact(Skip = "Sample test, not for execution")]
        public void ShouldPassMx6InstrumentWithDefaultSensors()
        {
            //This test is not passing currentlly because of improper gasendpoints.

            //Arrange
            InstrumentBumpTestAction action = Helper.GetBumpTestAction(DeviceType.MX6, new List<string> { "G0001", "G0002", "G0020" });
            Configuration.DockingStation = action.DockingStation;

            //Act
            InstrumentBumpTestOperation operation = new InstrumentBumpTestOperation(action);
            InstrumentBumpTestEvent returnEvent = operation.Execute() as InstrumentBumpTestEvent;

            //Assert that all default sensors passed bump test
            Xunit.Assert.NotNull(returnEvent);
            Xunit.Assert.True(returnEvent.GasResponses[0].Passed);
            Xunit.Assert.True(returnEvent.GasResponses[1].Passed);
            Xunit.Assert.True(returnEvent.GasResponses[2].Passed);
            Xunit.Assert.True(returnEvent.GasResponses[3].Passed);
        }

        public void Dispose()
        {
            
        }
    }
}