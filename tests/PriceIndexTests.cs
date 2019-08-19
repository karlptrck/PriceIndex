/* 
Copyright 2019 Novera Capital Inc.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

using Meadow.Contract;
using Meadow.JsonRpc.Types;
using Meadow.UnitTestTemplate;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System.Threading.Tasks;
using System;

namespace openSourcePriceIndex
{
    // Inherit from 'ContractTest' to be provided with an RpcClient, 
    // Accounts, and several other useful features.
    [TestClass]
    public class PriceIndexTest : ContractTest
    {

        PriceIndex _contract;
        int initPrice=  90010000;

        Meadow.Core.EthTypes.UInt256 maxNumberAuditors = 4;

        Meadow.Core.EthTypes.UInt256 fiatDecimals = 4;
        int margin=1;

        string fundName="Novera BTC Long Short"; 

        // Method is ran before each test (all tests are ran in isolation).
        // This is an appropriate area to do contract deployment.
        protected override async Task BeforeEach()
        {
            // Deploy our test contract
            _contract = await PriceIndex.New(initPrice,"meadowInit",4,(byte)fiatDecimals,RpcClient);
        }


        [TestMethod]
        public async Task ValidateDeployment()
        {
            //validate total price
            var totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(initPrice, totalBtcPriceUSDCents);

            //validate num of price agents
            var numberOfRegisteredPriceAgents = await _contract.numberOfRegisteredPriceAgents().Call();
            Assert.AreEqual(1, numberOfRegisteredPriceAgents);

            //validate max num of price agents
            var maxNumberPriceAgents = await _contract.maxNumberPriceAgents().Call();
            Assert.AreEqual(4, maxNumberPriceAgents);

            //validate that no funds are connected initially
            var numberOfConnectedFunds = await _contract.numberOfConnectedFunds().Call();
            Assert.AreEqual(0, numberOfConnectedFunds);

            //validate that deployer is the initial price agent
            var initPriceAgentAddress = await _contract.getRegisteredPriceAgent(0).Call();
            Assert.AreEqual(initPriceAgentAddress, Accounts[0]);

            //validate initial price agent report
            var BtcPriceReport = await _contract.PriceAgentReports(Accounts[0]).Call();
            Assert.AreEqual(initPrice, BtcPriceReport.price);
            int now = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            //validate decimals
            var decimals = await _contract.decimals().Call();
            Assert.AreEqual(fiatDecimals, decimals);

            //price report should have been made within the last 60sec
            Assert.AreEqual(true, BtcPriceReport.timestamp>=now-60  && BtcPriceReport.timestamp <= now );

            Assert.AreEqual("meadowInit", BtcPriceReport.source);
        }

        [TestMethod]
        public async Task ValidatePriceRegistrationUpdateFromInitNoFund()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash = await _contract.reportPrice(100000000,"meadowUpdate").SendTransaction(txParams);

            //validate total price
            var totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(100000000, totalBtcPriceUSDCents);

            //validate initial price agent report
            var BtcPriceReport = await _contract.PriceAgentReports(Accounts[0]).Call();
            int now = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            Assert.AreEqual(100000000, BtcPriceReport.price);
            //price report should have been made within the last 60sec
            Assert.AreEqual(true, BtcPriceReport.timestamp>=now-60  && BtcPriceReport.timestamp <= now );
            Assert.AreEqual("meadowUpdate", BtcPriceReport.source);
        }

        [TestMethod]
        public async Task ValidatePriceRegistrationUpdateEndFromInitNoFund()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash = await _contract.reportPrice(200000000,"meadowUpdate").SendTransaction(txParams);

            //validate total price
            var totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(200000000, totalBtcPriceUSDCents);

            //validate initial price agent report
            var BtcPriceReport = await _contract.PriceAgentReports(Accounts[0]).Call();
            int now = (int)(DateTime.UtcNow - new DateTime(1970, 1, 1)).TotalSeconds;

            Assert.AreEqual(200000000, BtcPriceReport.price);
             //price report should have been made within the last 60sec
            Assert.AreEqual(true, BtcPriceReport.timestamp>=now-60  && BtcPriceReport.timestamp <= now );

            Assert.AreEqual("meadowUpdate", BtcPriceReport.source);
        }


        [TestMethod]
        public async Task ValidateAddingNewPriceReportingAgent()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash = await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParams);

            //validate num of price agents
            var numberOfRegisteredPriceAgents = await _contract.numberOfRegisteredPriceAgents().Call();
            Assert.AreEqual(2, numberOfRegisteredPriceAgents);

            //validate total price (should not have changed from init)
            var totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(initPrice, totalBtcPriceUSDCents);


            //validate addition of new agent
            var newPriceAgentAddress = await _contract.getRegisteredPriceAgent(1).Call();
            Assert.AreEqual(newPriceAgentAddress, Accounts[1]);

            //validate new price agent report (that hasn't reported anything)
            var BtcPriceReport = await _contract.PriceAgentReports(Accounts[1]).Call();
            Assert.AreEqual(0, BtcPriceReport.price);
            Assert.AreEqual(0,BtcPriceReport.timestamp);
            Assert.AreEqual("", BtcPriceReport.source);
        }

        [TestMethod]
        public async Task ValidateGetInvalidReportingAgent()
        {
            var newPriceAgentAddress = await _contract.getRegisteredPriceAgent(10).Call();
            Assert.AreEqual(newPriceAgentAddress, "0x0000000000000000000000000000000000000000");
        }

        [TestMethod]
        public async Task ValidateRemovingNewReportingAgent()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash =await _contract.registerPriceAgent(Accounts[2]).SendTransaction(txParams);
            var txHash2 =await _contract.removePriceAgent(Accounts[2]).SendTransaction(txParams);


            //validate num of price agents
            var numberOfRegisteredPriceAgents = await _contract.numberOfRegisteredPriceAgents().Call();
            Assert.AreEqual(1, numberOfRegisteredPriceAgents);

            //validate total price (should not have changed from init)
            var totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(initPrice, totalBtcPriceUSDCents);

            //validate that address of added agent should not be present 
            var newPriceAgentAddress = await _contract.getRegisteredPriceAgent(1).Call();
            Assert.AreEqual("0x0000000000000000000000000000000000000000", newPriceAgentAddress);

        }


        [TestMethod]
        public async Task ValidateAddingReportingAgentInFreeSlot()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin

            await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParams);
            await _contract.registerPriceAgent(Accounts[2]).SendTransaction(txParams);

            await _contract.removePriceAgent(Accounts[1]).SendTransaction(txParams);

            //validate empty slot
            var newPriceAgentAddress = await _contract.getRegisteredPriceAgent(1).Call();
            Assert.AreEqual("0x0000000000000000000000000000000000000000", newPriceAgentAddress);

            await _contract.registerPriceAgent(Accounts[3]).SendTransaction(txParams);

            //validate filled slot
            newPriceAgentAddress = await _contract.getRegisteredPriceAgent(1).Call();
            Assert.AreEqual(Accounts[3], newPriceAgentAddress);


        }


        [TestMethod]
        public async Task ValidateConnectingFundNoMax()
        {
            //deploy a fund instance, but do no tests, that's something for the fund test suite
            Fund _FundContract;

            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //connect fund 
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

            //validate number of connected funds
            var numberOfConnectedFunds = await _contract.numberOfConnectedFunds().Call();
            Assert.AreEqual(1, numberOfConnectedFunds);

            //validate fund info
            var fundInfo = await _contract.funds(_FundContract.ContractAddress).Call();

            Assert.AreEqual(_FundContract.ContractAddress, fundInfo.fund);
            Assert.AreEqual(false, fundInfo.endPriceConditionsReached);
            Assert.AreEqual(0, fundInfo.endPrice); //default of 0 since it hasn't ended
            Assert.AreEqual(initPrice, fundInfo.strikePrice);
            
            var fundAddress=await _contract.getConnectedFundAddress(0).Call();
            Assert.AreEqual(_FundContract.ContractAddress, fundAddress);

            var hasFundEnded=await _contract.hasFundEnded(_FundContract.ContractAddress).Call();
            Assert.AreEqual(false, hasFundEnded);
        }

        [TestMethod]
        public async Task ValidateRequestForInvalidFund()
        {
            var fundAddress1=await _contract.getConnectedFundAddress(0).Call();
            Assert.AreEqual("0x0000000000000000000000000000000000000000", fundAddress1);

            Fund _FundContract;
            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //connect fund 
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

            var fundAddress2=await _contract.getConnectedFundAddress(4).Call();
            Assert.AreEqual("0x0000000000000000000000000000000000000000", fundAddress2);

        }

        [TestMethod]
        public async Task ValidatePriceAveragingNoFund()
        {

            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txParams2 = new TransactionParams { From = Accounts[1] }; // nonadmin1
            var txParams3 = new TransactionParams { From = Accounts[2] }; // nonadmin2

            await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParams);
            await _contract.registerPriceAgent(Accounts[2]).SendTransaction(txParams);

            await _contract.reportPrice(100011300,"meadow").SendTransaction(txParams2);

            //validate total price
            var totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(95010650, totalBtcPriceUSDCents); //averaging with truncation in the cents

            await _contract.reportPrice(130032100,"meadow").SendTransaction(txParams3);

            //validate total price
            totalBtcPriceUSDCents = await _contract.getPrice().Call();
            Assert.AreEqual(106684466, totalBtcPriceUSDCents); //averaging with truncation in the cents

        }

        [TestMethod]
        public async Task ValidateConnectingFundInEmptySlot()
        {
            //deploy a fund instance, but do no tests, that's something for the fund test suite
            Fund _FundContract;
            Fund _FundContract2;
            Fund _FundContract3;
            Fund _FundContract4;

            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);
            _FundContract2 = await Fund.New(_contract.ContractAddress,RpcClient);
            _FundContract3 =await Fund.New(_contract.ContractAddress,RpcClient);
            _FundContract4 = await Fund.New(_contract.ContractAddress,RpcClient);


            //connect fund 3 funds
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);
            await _contract.connectFund(_FundContract2.ContractAddress).SendTransaction(txParams);
            await _contract.connectFund(_FundContract3.ContractAddress).SendTransaction(txParams);

            //validate number of connected funds
            var numberOfConnectedFunds = await _contract.numberOfConnectedFunds().Call();
            Assert.AreEqual(3, numberOfConnectedFunds);

            
            //validate fund info
            var fundInfo = await _contract.funds(_FundContract.ContractAddress).Call();

            Assert.AreEqual(_FundContract.ContractAddress, fundInfo.fund);
            Assert.AreEqual(false, fundInfo.endPriceConditionsReached);
            Assert.AreEqual(0, fundInfo.endPrice); //default of 0 since it hasn't ended
            Assert.AreEqual(initPrice, fundInfo.strikePrice);
            
            var fundAddress=await _contract.getConnectedFundAddress(0).Call();
            Assert.AreEqual(_FundContract.ContractAddress, fundAddress);

            var hasFundEnded=await _contract.hasFundEnded(_FundContract.ContractAddress).Call();
            Assert.AreEqual(false, hasFundEnded);

           
        }


        [TestMethod]
        public async Task ValidateDoublingEventUponConnection()
        {

            //fund with strike price of initPrice
            Fund _FundContract;
            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //end
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash = await _contract.reportPrice(12345678,"meadowUpdate").SendTransaction(txParams);

            //connect fund 
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

            //validate fund info
            var fundInfo = await _contract.funds(_FundContract.ContractAddress).Call();

            Assert.AreEqual(_FundContract.ContractAddress, fundInfo.fund);
            Assert.AreEqual(true, fundInfo.endPriceConditionsReached);
            Assert.AreEqual(12345678, fundInfo.endPrice);
            Assert.AreEqual(initPrice, fundInfo.strikePrice);
            
            var hasFundEnded=await _contract.hasFundEnded(_FundContract.ContractAddress).Call();
            Assert.AreEqual(true, hasFundEnded);
        }
 
        [TestMethod]
        public async Task ValidateDoublingEventPostConnection()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin

            //fund with strike price of initPrice
            Fund _FundContract;
            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //connect fund
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

            //end
            var txHash = await _contract.reportPrice(12345678,"meadowUpdate").SendTransaction(txParams);

            //validate fund info
            var fundInfo = await _contract.funds(_FundContract.ContractAddress).Call();

            Assert.AreEqual(_FundContract.ContractAddress, fundInfo.fund);
            Assert.AreEqual(true, fundInfo.endPriceConditionsReached);
            Assert.AreEqual(12345678, fundInfo.endPrice);
            Assert.AreEqual(initPrice, fundInfo.strikePrice);
            
            var hasFundEnded=await _contract.hasFundEnded(_FundContract.ContractAddress).Call();
            Assert.AreEqual(true, hasFundEnded);
        }


        [TestMethod]
        public async Task ValidateEndAndReturn()
        {
            Meadow.Core.EthTypes.UInt256 newPrice = 12345678; 

            var txParams = new TransactionParams { From = Accounts[0] }; // admin

            //fund with strike price of initPrice
            Fund _FundContract;
            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //connect fund
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

            //end
            var txHash = await _contract.reportPrice(newPrice,"meadowUpdate").SendTransaction(txParams);

            //validate fund info
            var fundInfo = await _contract.funds(_FundContract.ContractAddress).Call();

            Assert.AreEqual(_FundContract.ContractAddress, fundInfo.fund);
            Assert.AreEqual(true, fundInfo.endPriceConditionsReached);
            Assert.AreEqual(newPrice, fundInfo.endPrice);
            Assert.AreEqual(initPrice, fundInfo.strikePrice);
            
            var hasFundEnded=await _contract.hasFundEnded(_FundContract.ContractAddress).Call();
            Assert.AreEqual(true, hasFundEnded);

            //back down from end
             await _contract.reportPrice(initPrice,"meadowUpdate").SendTransaction(txParams);

            //validate fund info again
            fundInfo = await _contract.funds(_FundContract.ContractAddress).Call();

            var EndPrice = await _contract.getEndPrice(_FundContract.ContractAddress).Call();

            Assert.AreEqual(_FundContract.ContractAddress, fundInfo.fund);
            Assert.AreEqual(true, fundInfo.endPriceConditionsReached);
            Assert.AreEqual(newPrice, fundInfo.endPrice); //end price was the peak
            Assert.AreEqual(newPrice, EndPrice); //end price was the peak

            Assert.AreEqual(initPrice, fundInfo.strikePrice);
            
            hasFundEnded=await _contract.hasFundEnded(_FundContract.ContractAddress).Call();
            Assert.AreEqual(true, hasFundEnded);
        }

        //fail cases
        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateRemovingAllValidReportingAgents_deleteAdmin()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.removePriceAgent(Accounts[0]).SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateRemovingAllValidReportingAgents_unitializedAgent()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash = await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParams);
            //new agent is uninitialzied, has not pushed any price yet
            //delete admin
            await _contract.removePriceAgent(Accounts[0]).SendTransaction(txParams);

        }



        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateAddingDuplicatePriceReportingAgent()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            var txHash = await _contract.registerPriceAgent(Accounts[0]).SendTransaction(txParams);

        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidatePriceRegistrationSecurity()
        {
            var txParams = new TransactionParams { From = Accounts[1] }; //not admin
            var txHash = await _contract.reportPrice(100000000,"meadow").SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateAddingPriceAgentSecurity()
        {
            var txParams = new TransactionParams { From = Accounts[1] }; //not admin
            var txHash = await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateRemovingPriceAgentSecurity()
        {
            var txParamsAdmin = new TransactionParams { From = Accounts[0] }; // admin
            var txParamsNotAdmin = new TransactionParams { From = Accounts[1] }; // not admin

            await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParamsAdmin);
            await _contract.removePriceAgent(Accounts[1]).SendTransaction(txParamsNotAdmin);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateAddingBeyondMaxPriceReportingAgents()
        {
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.registerPriceAgent(Accounts[1]).SendTransaction(txParams);
            await _contract.registerPriceAgent(Accounts[2]).SendTransaction(txParams);
            await _contract.registerPriceAgent(Accounts[3]).SendTransaction(txParams);
            await _contract.registerPriceAgent(Accounts[4]).SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateConnectingNullFund()
        {
            //connect fund 
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.connectFund("0x0000000000000000000000000000000000000000").SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateConnectingFundSecurity()
        {
           //deploy a fund instance, but do no tests, that's something for the fund test suite
            Fund _FundContract;
            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //connect fund 
            var txParams = new TransactionParams { From = Accounts[1] }; // notadmin
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateConnectingDuplicateFund()
        {
            //deploy a fund instance, but do no tests, that's something for the fund test suite
            Fund _FundContract;
            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //connect fund once
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

            //again
            await _contract.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateConnectingFund_decimalMismatch()
        {
            //deploy a fund instance, but do no tests, that's something for the fund test suite
            Fund _FundContract;

            _FundContract = await Fund.New(_contract.ContractAddress,RpcClient);

            //make new price index but with other decimals
            // Deploy our test contract
            Meadow.Core.EthTypes.UInt256 fiatDecimals2 = 5;
            PriceIndex _contract2 = await PriceIndex.New(initPrice,"meadowInit",4,(byte)fiatDecimals2,RpcClient);

            //connect fund 
            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract2.connectFund(_FundContract.ContractAddress).SendTransaction(txParams);

        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidatehasFundEnded_notAFund()
        {

            var txParams = new TransactionParams { From = Accounts[0] }; // admin

            //again
            await _contract.hasFundEnded(Accounts[0]).SendTransaction(txParams);
        }

        [TestMethod]
        [ExpectedException(typeof(Meadow.CoverageReport.Debugging.ContractExecutionException))]
        public async Task ValidateResetFund_notConnectedFund()
        {

            var txParams = new TransactionParams { From = Accounts[0] }; // admin
            await _contract.resetFundStatus().SendTransaction(txParams);

        }


    }
}