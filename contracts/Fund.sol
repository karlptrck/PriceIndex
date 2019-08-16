pragma solidity ^0.4.24;
import "./PriceIndex.sol";

/// @title Novera Fund Example
/// @author Jose Miguel Herrera
/// @notice This is only an example of the basic features you need in a fund to make it work with a price index.

/* 
Copyright 2019 Novera Capital Inc.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
*/

contract Fund {
    PriceIndex public price_index;
    uint256 public strikePrice; //strike price of the fund
    uint8 public decimals; //the precision of the fiat representation

    constructor(address priceIndexAddress){
        price_index=PriceIndex(priceIndexAddress);
        decimals=price_index.decimals();
        strikePrice=price_index.getPrice();
    }

    function wouldReachEndConditions(uint256 atPrice) public view returns(bool){
        //add business logic that maps a price to a boolean describing if the fund *would* end at that price.
        if(atPrice==12345678){
            return true;
        }else{
            return false;
        }
    }
}