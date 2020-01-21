/* 
Copyright 2019 Novera Capital Inc.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

pragma solidity ^0.4.24;
import "./SafeMath.sol"; //openzeppelin
import "./Ownable.sol"; //openzeppelin
import "./Fund.sol";

/// @title Novera Price Index
/// @author Jose Miguel Herrera
/// @notice You can use this contract to create a price oracle for a financial asset. Prices come from N registered price agents and averaged.
/// @notice You can use this contract independently, or connected to a "fund" which reads the price and has an end price, at which the fund ends.
/// @dev With mythX static analysis, you get warning SWC-128 (DoS With Block Gas Limit) due to the looping over connectedFundAddresses in reportPrice(). Since adding new funds is not a user facing function, there is no DoS threat...
/// @dev ... However, this means that there IS an upper ceiling on how many funds can be connected to one price index.

contract PriceIndex is Ownable {
    string public baseTicker;
    string public quoteTicker;
    mapping(address => FundInfo) public funds;

    address[] internal connectedFundAddresses;
    mapping(address => uint) public connectedFundIndex;
    mapping(address => bool) internal isConnectedFund;
    uint256 public numberOfConnectedFunds;

    using SafeMath for uint256;

    uint256 public masterPrice;
    address[] internal registeredPriceAgents;
    uint256 public numberOfRegisteredPriceAgents;
    uint256 public maxNumberPriceAgents;
    uint256 public decimals;

    mapping(address => bool) internal isRegisteredPriceAgent;
    mapping(address => priceReport) public PriceAgentReports;

    event LogPriceUpdate(uint256 _newPrice, uint256 timestamp);
    event changeInPriceAgents(uint256 _newNumberOfPriceAgents, address changer);
    event fundEndAlert(
        uint256 timestamp,
        uint256 endPrice,
        address fundAddress
    );

    /// @notice Creates a PriceIndex instance.
    /// @dev In the future, both the asset name and the currency used to track it will be included as parameters.
    /// @param initialPrice The initial price of the asset.
    /// @param initialPriceSource Where initialPrice is coming from.
    /// @param _maxNumberPriceAgents The max nnumber of price agents this price index will allow.
    /// @param _decimals The decimal precision used to represent the price of the asset.
    constructor(
        uint256 initialPrice,
        string initialPriceSource,
        uint256 _maxNumberPriceAgents,
        uint256 _decimals,
        string _baseTicker,
        string _quoteTicker
    ) public {
        numberOfRegisteredPriceAgents = 0;
        maxNumberPriceAgents = _maxNumberPriceAgents;
        registerPriceAgent(msg.sender);

        PriceAgentReports[msg.sender] = priceReport(
            initialPrice,
            now,
            initialPriceSource
        );

        masterPrice = initialPrice;
        numberOfConnectedFunds = 0;
        decimals = _decimals;
        baseTicker = _baseTicker;
        quoteTicker = _quoteTicker;
        emit LogPriceUpdate(masterPrice, now);
    }

    /// @notice Connects a Fund to this price index - the fund starts tracking it.
    /// @notice Only callable by the contract owner.
    /// @dev Right now there's no way to disconnect a fund, this feature could be added in the future.
    /// @param fundAddress The address of the fund being created.
    function connectFund(address fundAddress) public onlyOwner {
        require(fundAddress != 0);
        require(!isConnectedFund[fundAddress]);
        require(Fund(fundAddress).decimals() == decimals);

        isConnectedFund[fundAddress] = true;

        uint fundIndex = connectedFundAddresses.push(fundAddress);
        connectedFundIndex[fundAddress] = fundIndex;

        funds[fundAddress] = FundInfo(Fund(fundAddress), false, 0, 0);
        funds[fundAddress].strikePrice = funds[fundAddress].fund.strikePrice();

        numberOfConnectedFunds = numberOfConnectedFunds.add(1);

        //what if new connected fund here has reached max conditions already??
        if (reachedMaxConditions(fundAddress)) {
            handleMaxConditionForFund(fundAddress);
        }

    }

    function disconnectFund(address fundAddress) public onlyOwner {
        require(fundAddress != 0);
        require(isConnectedFund[fundAddress]);

        isConnectedFund[fundAddress] = false;

        uint indexToDelete = connectedFundIndex[fundAddress];

        if(indexToDelete < numberOfConnectedFunds){
            address lastItem = connectedFundAddresses[connectedFundAddresses.length-1];

            // move the last element to the deleted spot
            connectedFundAddresses[indexToDelete] = lastItem;

            // update the last element's key index
            connectedFundIndex[lastItem] = indexToDelete;
        } else {
            // last item has to be deleted, reset the key index back to 0
            connectedFundIndex[fundAddress] = 0;
            indexToDelete--;
        }

        delete connectedFundAddresses[indexToDelete];

        connectedFundAddresses.length--;

        numberOfConnectedFunds = numberOfConnectedFunds.sub(1);
    }

    /// @notice Retrieves the address of one of the connected funds.
    /// @param index The index of the Ith connected fund.
    /// @return The address of the Ith connected fund.
    function getConnectedFundAddress(uint256 index)
        public
        view
        returns (address)
    {
        if (
            numberOfConnectedFunds == 0 ||
            index > connectedFundAddresses.length.sub(1)
        ) {
            return 0;
        } else {
            return connectedFundAddresses[index];
        }
    }

    /// @notice Retrieves the address of one of the registered price agents.
    /// @param index The index of the Ith registered agent.
    /// @return The address of the Ith registered agent.
    function getRegisteredPriceAgent(uint256 index)
        public
        view
        returns (address)
    {
        if (
            numberOfRegisteredPriceAgents == 0 ||
            index > registeredPriceAgents.length.sub(1)
        ) {
            return 0;
        } else {
            return registeredPriceAgents[index];
        }
    }

    /// @notice Retrieves the current price of the tracked asset.
    /// @dev Designed for the use of a Fund contract (external requests), but can be used publically (would be the same as calling masterPrice())
    /// @return If called by Fund that has reached its end conditions, then returns the price that cuased the fund to end. Otherwise, returns CURRENT the price of the asset.
    function getPrice() public view returns (uint256) {
        //if I have a fund connected that has reached its end conditions, give it end price
        if (
            isConnectedFund[msg.sender] &&
            funds[msg.sender].endPriceConditionsReached
        ) {
            return funds[msg.sender].endPrice;
        } else {
            return masterPrice;
        }
    }

    /// @notice Updates current price of the tracked asset and handles the ending of funds.
    /// @param reportedPrice The reported price.
    /// @param reportedPriceSource Where reportedPrice comes from.
    /// @dev Currently averages the prices, future improvement to moving averages possible.
    /// @dev Analysis needs to be done on this function to see how gas usage increases with the growth of more connected fund addresses, as it may reach a point of self-inflicted DoS.
    /// @dev The above is an argument for having the ability to DISCONNECT funds.
    function reportPrice(uint256 reportedPrice, string reportedPriceSource)
        public
    {
        require(isRegisteredPriceAgent[msg.sender]); //needs to be registered price agent
        PriceAgentReports[msg.sender] = priceReport(
            reportedPrice,
            now,
            reportedPriceSource
        );
        aggregatePrice();
        checkAndUpdateFunds();
    }

    /// @dev Internal function used to check if the current price, newly updated, causes the end of a fund.
    /// @param fundAddress The address of the fund that is being checked.
    /// @return true if the current price has caused the fund to reach its max/end conditions. False otherwise.
    function reachedMaxConditions(address fundAddress)
        internal
        view
        returns (bool)
    {
        require(isConnectedFund[fundAddress]);
        //modified so that price index asks a particular fund if it has reached its custom max conditions.
        return funds[fundAddress].fund.wouldReachEndConditions(masterPrice);
    }

    /// @dev Internal function used to handle when a price update causes the end of a fund.
    /// @param fundAddress The address of the fund that has ended due to a price update
    function handleMaxConditionForFund(address fundAddress) internal {
        emit fundEndAlert(now, masterPrice, fundAddress);
        funds[fundAddress].endPriceConditionsReached = true;
        funds[fundAddress].endPrice = masterPrice;
    }

    /// @notice Checks if a connected fund has ended.
    /// @param fundAddress The address of the fund that is going to be checked.
    /// @return true if the fund has ended. false otherwise.
    function hasFundEnded(address fundAddress) public view returns (bool) {
        require(isConnectedFund[fundAddress]);
        return funds[fundAddress].endPriceConditionsReached;
    }

    /// @notice Retrieves the price that caused a fund to end.
    /// @param fundAddress The address of the fund that ended.
    /// @return the price that caused the fund to end.
    function getEndPrice(address fundAddress) public view returns (uint256) {
        require(hasFundEnded(fundAddress));
        return funds[fundAddress].endPrice;
    }

    /// @dev Only callable by a Fund contract (external request) that has reached its end conditions/has ended.
    /// @dev Resets the end status of a fun and retreives the new strike price.
    function resetFundStatus() public {
        require(
            isConnectedFund[msg.sender] &&
                funds[msg.sender].endPriceConditionsReached
        );
        funds[msg.sender].endPriceConditionsReached = false;
        funds[msg.sender].endPrice = 0;
        funds[msg.sender].strikePrice = funds[msg.sender].fund.strikePrice();
    }

    /// @dev Internal function that retrieves all current price reports and averages them, updating the master price.
    /// @dev This is where moving average functionality could be added instead of simple averaging.
    /// @dev Warning: does not work if there are no valid (have reported a price) reports/agents. This is enforced in the removal of agents.
    function aggregatePrice() private {
        uint256 aggPrice = 0;
        uint256 numValidPrices = 0;

        for (uint256 i = 0; i < registeredPriceAgents.length; i = i.add(1)) {
            if (
                registeredPriceAgents[i] != 0 &&
                PriceAgentReports[registeredPriceAgents[i]].timestamp != 0
            ) {
                numValidPrices = numValidPrices.add(1);
                aggPrice = aggPrice.add(
                    PriceAgentReports[registeredPriceAgents[i]].price
                );
            }
        }

        if (numValidPrices == 0) {
            //THIS SHOULD NEVER HAPPEN, AND IS GUARANTEED BY THE REMOVE PRICE AGENT FUNC, THERE SHALL BE ALWAYS 1 AGENT
            revert(
                "Price cannot be aggregated because there are no valid prices."
            );
        } else {
            aggPrice = aggPrice.div(numValidPrices);
        }

        emit LogPriceUpdate(aggPrice, now);
        masterPrice = aggPrice;
    }

    /// @dev Internal function that checks if the previous price update would end a fund, and triggers the end process
    function checkAndUpdateFunds() private {
        for (uint256 i = 0; i < connectedFundAddresses.length; i = i.add(1)) {
            if (
                isConnectedFund[connectedFundAddresses[i]] &&
                !hasFundEnded(connectedFundAddresses[i]) &&
                reachedMaxConditions(connectedFundAddresses[i])
            ) {
                handleMaxConditionForFund(connectedFundAddresses[i]);
            }
        }
    }

    /// @notice Registers an address as a registered price agent for this price index.
    /// @notice Only callable by the contract owner.
    /// @param newAgent The address of the price agent to be registered.
    function registerPriceAgent(address newAgent) public onlyOwner {
        require(!isRegisteredPriceAgent[newAgent]); //prevent double registrations
        require(numberOfRegisteredPriceAgents < maxNumberPriceAgents); //no more than max
        isRegisteredPriceAgent[newAgent] = true;
        bool foundEmptySlot = false;

        for (uint256 i = 0; i < registeredPriceAgents.length; i = i.add(1)) {
            if (registeredPriceAgents[i] == 0) {
                registeredPriceAgents[i] = newAgent; //add agent in first free slot
                foundEmptySlot = true;
                break;
            }
        }

        if (!foundEmptySlot) {
            registeredPriceAgents.push(newAgent);
        }

        numberOfRegisteredPriceAgents = numberOfRegisteredPriceAgents.add(1);
        emit changeInPriceAgents(numberOfRegisteredPriceAgents, msg.sender);
    }

    /// @notice Removes an address as a registered price agent for this price index. Also removes the agent's price report, recalculating the master price.
    /// @notice Only callable by the contract owner.
    /// @dev Warning: you cannot remove the LAST valid (has reported a price) price reporting agent.
    /// @param agentToRemove The address of the price agent to be removed.
    function removePriceAgent(address agentToRemove) public onlyOwner {
        //require(agentToRemove!=owner()); //cannot remove contract owner (note: this is something that needs to be further discussed)
        //require(numberOfRegisteredPriceAgents>1);
        //above were previous attempts to prevent a bad state when the last valid agent was deleted, but now it's being done in aggregatePrice
        isRegisteredPriceAgent[agentToRemove] = false;

        delete PriceAgentReports[agentToRemove];

        for (uint256 i = 0; i < registeredPriceAgents.length; i = i.add(1)) {
            if (registeredPriceAgents[i] == agentToRemove) {
                delete registeredPriceAgents[i]; //leaves a gap of 0
            }
        }

        aggregatePrice();
        numberOfRegisteredPriceAgents = numberOfRegisteredPriceAgents.sub(1);
        emit changeInPriceAgents(numberOfRegisteredPriceAgents, msg.sender);

    }

    struct priceReport {
        uint256 price;
        uint256 timestamp;
        string source;
    }

    struct FundInfo {
        Fund fund;
        bool endPriceConditionsReached;
        uint256 endPrice;
        uint256 strikePrice;
    }

}
