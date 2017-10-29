﻿using Binance.Account;
using Binance.Account.Orders;
using Binance.Api.Json;
using Binance.Market;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Binance.Api
{
    /// <summary>
    /// Binance API <see cref="IBinanceApi"/> implementation.
    /// </summary>
    public class BinanceApi : IBinanceApi
    {
        #region Public Constants

        public const long NullId = -1;

        #endregion Public Constants

        #region Public Properties

        public IBinanceJsonApi JsonApi { get; }

        #endregion Public Properties

        #region Constructors

        /// <summary>
        /// Default constructor provides default rate limiter implementation
        /// and no configuration options or logging functionality.
        /// </summary>
        public BinanceApi()
            : this(new BinanceJsonApi())
        { }

        /// <summary>
        /// Constructor.
        /// </summary>
        /// <param name="jsonApi"></param>
        public BinanceApi(IBinanceJsonApi jsonApi)
        {
            Throw.IfNull(jsonApi, nameof(jsonApi));

            JsonApi = jsonApi;
        }

        #endregion Constructors

        #region Connectivity

        public virtual async Task<bool> PingAsync(CancellationToken token = default)
        {
            return await JsonApi
                .PingAsync(token).ConfigureAwait(false)
                == BinanceJsonApi.SuccessfulTestResponse;
        }

        public virtual async Task<long> GetTimestampAsync(CancellationToken token = default)
        {
            var json = await JsonApi.GetServerTimeAsync(token)
                .ConfigureAwait(false);

            try
            {
                return JObject.Parse(json)["serverTime"].Value<long>();
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetTimestampAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        #endregion Connectivity

        #region Market Data

        public virtual async Task<OrderBook> GetOrderBookAsync(string symbol, int limit = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetOrderBookAsync(symbol, limit, token)
                .ConfigureAwait(false);

            try
            {
                var jObject = JObject.Parse(json);

                var lastUpdateId = jObject["lastUpdateId"].Value<long>();

                var bids = jObject["bids"].Select(entry => (entry[0].Value<decimal>(), entry[1].Value<decimal>())).ToList();
                var asks = jObject["asks"].Select(entry => (entry[0].Value<decimal>(), entry[1].Value<decimal>())).ToList();

                return new OrderBook(symbol.FormatSymbol(), lastUpdateId, bids, asks);
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOrderBookAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<AggregateTrade>> GetAggregateTradesAsync(string symbol, int limit = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetAggregateTradesAsync(symbol, NullId, limit, 0, 0, token)
                .ConfigureAwait(false);

            try { return DeserializeAggregateTrades(symbol, json); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetAggregateTradesAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<AggregateTrade>> GetAggregateTradesFromAsync(string symbol, long fromId, int limit = default, CancellationToken token = default)
        {
            if (fromId < 0)
                throw new ArgumentException("ID must not be less than 0.", nameof(fromId));

            var json = await JsonApi.GetAggregateTradesAsync(symbol, fromId, limit, 0, 0, token)
                .ConfigureAwait(false);

            try { return DeserializeAggregateTrades(symbol, json); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetAggregateTradesFromAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<AggregateTrade>> GetAggregateTradesInAsync(string symbol, long startTime, long endTime, CancellationToken token = default)
        {
            if (startTime <= 0)
                throw new ArgumentException("Timestamp must be greater than 0.", nameof(startTime));
            if (endTime < startTime)
                throw new ArgumentException($"Timestamp must not be less than {nameof(startTime)} ({startTime}).", nameof(endTime));

            var json = await JsonApi.GetAggregateTradesAsync(symbol, NullId, 0, startTime, endTime, token)
                .ConfigureAwait(false);

            try { return DeserializeAggregateTrades(symbol, json); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetAggregateTradesInAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<Candlestick>> GetCandlesticksAsync(string symbol, KlineInterval interval, int limit = default, long startTime = default, long endTime = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetCandlesticksAsync(symbol, interval, limit, startTime, endTime, token)
                .ConfigureAwait(false);

            try
            {
                var jArray = JArray.Parse(json);

                return jArray.Select(item => new Candlestick(
                        symbol.FormatSymbol(),      // symbol
                        interval,                   // interval
                        item[0].Value<long>(),      // open time
                        item[1].Value<decimal>(),   // open
                        item[2].Value<decimal>(),   // high
                        item[3].Value<decimal>(),   // low
                        item[4].Value<decimal>(),   // close
                        item[5].Value<decimal>(),   // volume
                        item[6].Value<long>(),      // close time
                        item[7].Value<decimal>(),   // quote asset volume
                        item[8].Value<long>(),      // number of trades
                        item[9].Value<decimal>(),   // taker buy base asset volume
                        item[10].Value<decimal>()   // taker buy quote asset volume
                    )).ToList();
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetCandlesticksAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<Symbol24HourStatistics> Get24HourStatisticsAsync(string symbol, CancellationToken token = default)
        {
            var json = await JsonApi.Get24HourStatisticsAsync(symbol, token)
                .ConfigureAwait(false);

            try
            {
                var jObject = JObject.Parse(json);

                return new Symbol24HourStatistics(
                    symbol.FormatSymbol(),
                    jObject["priceChange"].Value<decimal>(),
                    jObject["priceChangePercent"].Value<decimal>(),
                    jObject["weightedAvgPrice"].Value<decimal>(),
                    jObject["prevClosePrice"].Value<decimal>(),
                    jObject["lastPrice"].Value<decimal>(),
                    jObject["bidPrice"].Value<decimal>(),
                    jObject["askPrice"].Value<decimal>(),
                    jObject["openPrice"].Value<decimal>(),
                    jObject["highPrice"].Value<decimal>(),
                    jObject["lowPrice"].Value<decimal>(),
                    jObject["volume"].Value<decimal>(),
                    jObject["openTime"].Value<long>(),
                    jObject["closeTime"].Value<long>(),
                    jObject["firstId"].Value<long>(),
                    jObject["lastId"].Value<long>(),
                    jObject["count"].Value<long>());
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(Get24HourStatisticsAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<SymbolPrice>> GetPricesAsync(CancellationToken token = default)
        {
            var json = await JsonApi.GetPricesAsync(token)
                .ConfigureAwait(false);

            try
            {
                return JArray.Parse(json).Select(item => new SymbolPrice(item["symbol"].Value<string>(), item["price"].Value<decimal>())).ToList();
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetPricesAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<OrderBookTop>> GetOrderBookTopsAsync(CancellationToken token = default)
        {
            var json = await JsonApi.GetOrderBookTopsAsync(token)
                .ConfigureAwait(false);

            try
            {
                var jArray = JArray.Parse(json);

                return jArray.Select(item => new OrderBookTop(
                        item["symbol"].Value<string>(),
                        item["bidPrice"].Value<decimal>(),
                        item["bidQty"].Value<decimal>(),
                        item["askPrice"].Value<decimal>(),
                        item["askQty"].Value<decimal>())).ToList();
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOrderBookTopsAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        #endregion Market Data

        #region Account

        public virtual async Task<Order> PlaceAsync(IBinanceApiUser user, ClientOrder clientOrder, long recvWindow = default, CancellationToken token = default)
        {
            Throw.IfNull(clientOrder, nameof(clientOrder));

            var limitOrder = clientOrder as LimitOrder;

            var order = new Order(user)
            {
                Symbol = clientOrder.Symbol.FormatSymbol(),
                OriginalQuantity = clientOrder.Quantity,
                Price = limitOrder?.Price ?? 0,
                Side = clientOrder.Side,
                Type = clientOrder.Type,
                Status = OrderStatus.New,
                TimeInForce = limitOrder?.TimeInForce ?? TimeInForce.GTC
            };

            // Place the order.
            var json = await JsonApi.PlaceOrderAsync(user, clientOrder.Symbol, clientOrder.Side, clientOrder.Type, clientOrder.Quantity, limitOrder?.Price ?? 0, clientOrder.Id, limitOrder?.TimeInForce, clientOrder.StopPrice, clientOrder.IcebergQuantity, recvWindow, false, token);

            try
            {
                FillOrder(order, JObject.Parse(json));

                // Update client order properties.
                clientOrder.Id = order.ClientOrderId;
                clientOrder.Timestamp = order.Timestamp;
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(PlaceAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            return order;
        }

        public virtual async Task TestPlaceAsync(IBinanceApiUser user, ClientOrder clientOrder, long recvWindow = default, CancellationToken token = default)
        {
            Throw.IfNull(clientOrder, nameof(clientOrder));

            var limitOrder = clientOrder as LimitOrder;

            // Place the order.
            var json = await JsonApi.PlaceOrderAsync(user, clientOrder.Symbol, clientOrder.Side, clientOrder.Type, clientOrder.Quantity, limitOrder?.Price ?? 0, clientOrder.Id, limitOrder?.TimeInForce, clientOrder.StopPrice, clientOrder.IcebergQuantity, recvWindow, true, token);

            if (json != BinanceJsonApi.SuccessfulTestResponse)
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(TestPlaceAsync)} failed order placement test.");
        }

        public virtual async Task<Order> GetOrderAsync(IBinanceApiUser user, string symbol, long orderId, long recvWindow = default, CancellationToken token = default)
        {
            // Get order using order ID.
            var json = await JsonApi.GetOrderAsync(user, symbol, orderId, null, recvWindow, token)
                .ConfigureAwait(false);

            var order = new Order(user);

            try { FillOrder(order, JObject.Parse(json)); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOrderAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            return order;
        }

        public virtual async Task<Order> GetOrderAsync(IBinanceApiUser user, string symbol, string origClientOrderId, long recvWindow = default, CancellationToken token = default)
        {
            // Get order using original client order ID.
            var json = await JsonApi.GetOrderAsync(user, symbol, NullId, origClientOrderId, recvWindow, token)
                .ConfigureAwait(false);

            var order = new Order(user);

            try { FillOrder(order, JObject.Parse(json)); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOrderAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            return order;
        }

        public virtual async Task<Order> GetAsync(Order order, long recvWindow = default, CancellationToken token = default)
        {
            Throw.IfNull(order, nameof(order));

            // Get order using order ID.
            var json = await JsonApi.GetOrderAsync(order.User, order.Symbol, order.Id, null, recvWindow, token)
                .ConfigureAwait(false);

            try { FillOrder(order, JObject.Parse(json)); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOrderAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            return order;
        }

        public virtual async Task<string> CancelOrderAsync(IBinanceApiUser user, string symbol, long orderId, string newClientOrderId = null, long recvWindow = default, CancellationToken token = default)
        {
            if (orderId < 0)
                throw new ArgumentException("ID must not be less than 0.", nameof(orderId));

            // Cancel order using order ID.
            var json = await JsonApi.CancelOrderAsync(user, symbol, orderId, null, newClientOrderId, recvWindow, token)
                .ConfigureAwait(false);

            try { return JObject.Parse(json)["clientOrderId"].Value<string>(); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(CancelOrderAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<string> CancelOrderAsync(IBinanceApiUser user, string symbol, string origClientOrderId, string newClientOrderId = null, long recvWindow = default, CancellationToken token = default)
        {
            Throw.IfNullOrWhiteSpace(origClientOrderId, nameof(origClientOrderId));

            // Cancel order using original client order ID.
            var json = await JsonApi.CancelOrderAsync(user, symbol, NullId, origClientOrderId, newClientOrderId, recvWindow, token)
                .ConfigureAwait(false);

            try { return JObject.Parse(json)["clientOrderId"].Value<string>(); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(CancelOrderAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual Task<string> CancelAsync(Order order, string newClientOrderId = null, long recvWindow = default, CancellationToken token = default)
        {
            Throw.IfNull(order, nameof(order));

            // Cancel order using order ID.
            return CancelOrderAsync(order.User, order.Symbol, order.Id, newClientOrderId, recvWindow, token);
        }

        public virtual async Task<IEnumerable<Order>> GetOpenOrdersAsync(IBinanceApiUser user, string symbol, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetOpenOrdersAsync(user, symbol, recvWindow, token)
                .ConfigureAwait(false);

            try
            {
                var jArray = JArray.Parse(json);

                var orders = new List<Order>();
                foreach (var jToken in jArray)
                {
                    var order = new Order(user);

                    FillOrder(order, jToken);

                    orders.Add(order);
                }
                return orders;
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOpenOrdersAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<Order>> GetOrdersAsync(IBinanceApiUser user, string symbol, long orderId = NullId, int limit = default, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetOrdersAsync(user, symbol, orderId, limit, recvWindow, token)
                .ConfigureAwait(false);

            try
            {
                var jArray = JArray.Parse(json);

                var orders = new List<Order>();
                foreach (var jToken in jArray)
                {
                    var order = new Order(user);

                    FillOrder(order, jToken);

                    orders.Add(order);
                }
                return orders;
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetOrdersAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<AccountInfo> GetAccountInfoAsync(IBinanceApiUser user, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetAccountInfoAsync(user, recvWindow, token)
                .ConfigureAwait(false);

            try
            {
                var jObject = JObject.Parse(json);

                var commissions = new AccountCommissions(
                    jObject["makerCommission"].Value<int>(),
                    jObject["takerCommission"].Value<int>(),
                    jObject["buyerCommission"].Value<int>(),
                    jObject["sellerCommission"].Value<int>());

                var status = new AccountStatus(
                    jObject["canTrade"].Value<bool>(),
                    jObject["canWithdraw"].Value<bool>(),
                    jObject["canDeposit"].Value<bool>());

                var balances = jObject["balances"].Select(entry => new AccountBalance(entry["asset"].Value<string>(), entry["free"].Value<decimal>(), entry["locked"].Value<decimal>())).ToList();

                return new AccountInfo(user, commissions, status, balances);
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetAccountInfoAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task<IEnumerable<AccountTrade>> GetTradesAsync(IBinanceApiUser user, string symbol, long fromId = NullId, int limit = default, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetTradesAsync(user, symbol, fromId, limit, recvWindow, token)
                .ConfigureAwait(false);

            try
            {
                var jArray = JArray.Parse(json);

                return jArray.Select(jToken => new AccountTrade(
                        symbol.FormatSymbol(),
                        jToken["id"].Value<long>(),
                        jToken["price"].Value<decimal>(),
                        jToken["qty"].Value<decimal>(),
                        jToken["commission"].Value<decimal>(),
                        jToken["commissionAsset"].Value<string>(),
                        jToken["time"].Value<long>(),
                        jToken["isBuyer"].Value<bool>(),
                        jToken["isMaker"].Value<bool>(),
                        jToken["isBestMatch"].Value<bool>())).ToList();
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetTradesAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public virtual async Task WithdrawAsync(IBinanceApiUser user, string asset, string address, decimal amount, string name = null, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.WithdrawAsync(user, asset, address, amount, name, recvWindow, token)
                .ConfigureAwait(false);

            bool success;
            string message;

            try
            {
                var jObject = JObject.Parse(json);

                success = jObject["success"].Value<bool>();
                message = jObject["msg"]?.Value<string>();
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(WithdrawAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            if (!success)
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(WithdrawAsync)} failed: \"{message ?? "[No Message]"}\"");
        }

        public virtual async Task<IEnumerable<Deposit>> GetDepositsAsync(IBinanceApiUser user, string asset, DepositStatus? status = null, long startTime = default, long endTime = default, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetDepositsAsync(user, asset, status, startTime, endTime, recvWindow, token)
                .ConfigureAwait(false);

            bool success;
            var deposits = new List<Deposit>();

            try
            {
                var jObject = JObject.Parse(json);

                success = jObject["success"].Value<bool>();

                if (success)
                {
                    var depositList = jObject["depositList"];

                    if (depositList != null)
                    {
                        deposits.AddRange(
                            depositList.Select(jToken => new Deposit(
                                jToken["asset"].Value<string>(),
                                jToken["amount"].Value<decimal>(),
                                jToken["insertTime"].Value<long>(),
                                (DepositStatus)jToken["status"].Value<int>())));
                    }
                }
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetDepositsAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            if (!success)
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetDepositsAsync)} failed.");

            return deposits;
        }

        public virtual async Task<IEnumerable<Withdrawal>> GetWithdrawalsAsync(IBinanceApiUser user, string asset, WithdrawalStatus? status = null, long startTime = default, long endTime = default, long recvWindow = default, CancellationToken token = default)
        {
            var json = await JsonApi.GetWithdrawalsAsync(user, asset, status, startTime, endTime, recvWindow, token)
                .ConfigureAwait(false);

            bool success;
            var withdrawals = new List<Withdrawal>();

            try
            {
                var jObject = JObject.Parse(json);

                success = jObject["success"].Value<bool>();

                if (success)
                {
                    var withdrawList = jObject["withdrawList"];

                    if (withdrawList != null)
                    {
                        withdrawals.AddRange(
                            withdrawList.Select(jToken => new Withdrawal(
                                jToken["asset"].Value<string>(),
                                jToken["amount"].Value<decimal>(),
                                jToken["applyTime"].Value<long>(),
                                (WithdrawalStatus)jToken["status"].Value<int>(),
                                jToken["address"].Value<string>(),
                                jToken["txId"]?.Value<string>())));
                    }
                }
            }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetWithdrawalsAsync)} failed to parse JSON api response: \"{json}\"", e);
            }

            if (!success)
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(GetWithdrawalsAsync)} failed.");

            return withdrawals;
        }

        #endregion Account

        #region User Data Stream

        public async Task<string> UserStreamStartAsync(IBinanceApiUser user, CancellationToken token = default)
        {
            var json = await JsonApi.UserStreamStartAsync(user, token)
                .ConfigureAwait(false);

            try { return JObject.Parse(json)["listenKey"].Value<string>(); }
            catch (Exception e)
            {
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(UserStreamStartAsync)} failed to parse JSON api response: \"{json}\"", e);
            }
        }

        public async Task UserStreamKeepAliveAsync(IBinanceApiUser user, string listenKey, CancellationToken token = default)
        {
            var json = await JsonApi.UserStreamKeepAliveAsync(user, listenKey, token)
                .ConfigureAwait(false);

            if (json != BinanceJsonApi.SuccessfulTestResponse)
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(UserStreamKeepAliveAsync)} failed.");
        }

        public async Task UserStreamCloseAsync(IBinanceApiUser user, string listenKey, CancellationToken token = default)
        {
            var json = await JsonApi.UserStreamCloseAsync(user, listenKey, token)
                .ConfigureAwait(false);

            if (json != BinanceJsonApi.SuccessfulTestResponse)
                throw new BinanceApiException($"{nameof(BinanceApi)}.{nameof(UserStreamCloseAsync)} failed.");
        }

        #endregion User Data Stream

        #region Private Methods

        /// <summary>
        /// Deserialize aggregate trade.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="json"></param>
        /// <returns></returns>
        private static IEnumerable<AggregateTrade> DeserializeAggregateTrades(string symbol, string json)
        {
            var jArray = JArray.Parse(json);

            return jArray.Select(item => new AggregateTrade(
                    symbol.FormatSymbol(),
                    item["a"].Value<long>(),    // ID
                    item["p"].Value<decimal>(), // price
                    item["q"].Value<decimal>(), // quantity
                    item["f"].Value<long>(),    // first trade ID
                    item["l"].Value<long>(),    // last trade ID
                    item["T"].Value<long>(),    // timestamp
                    item["m"].Value<bool>(),    // is buyer maker
                    item["M"].Value<bool>()))   // is best price match
                .ToList();
        }

        /// <summary>
        /// Deserialize order.
        /// </summary>
        /// <param name="order"></param>
        /// <param name="jToken"></param>
        private static void FillOrder(Order order, JToken jToken)
        {
            order.Symbol = jToken["symbol"].Value<string>();
            order.Id = jToken["orderId"].Value<long>();
            order.ClientOrderId = jToken["clientOrderId"].Value<string>();

            order.Timestamp = (jToken["time"] ?? jToken["transactTime"]).Value<long>();

            order.Price = jToken["price"].Value<decimal>();
            order.OriginalQuantity = jToken["origQty"].Value<decimal>();
            order.ExecutedQuantity = jToken["executedQty"].Value<decimal>();
            order.Status = jToken["status"].Value<string>().ConvertOrderStatus();
            order.TimeInForce = jToken["timeInForce"].Value<string>().ConvertTimeInForce();
            order.Type = jToken["type"].Value<string>().ConvertOrderType();
            order.Side = jToken["side"].Value<string>().ConvertOrderSide();
            order.StopPrice = jToken["stopPrice"]?.Value<decimal>() ?? 0;
            order.IcebergQuantity = jToken["icebergQty"]?.Value<decimal>() ?? 0;
        }

        #endregion Private Methods

        #region IDisposable

        private bool _disposed;

        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                JsonApi?.Dispose();
            }

            _disposed = true;
        }

        public void Dispose()
        {
            Dispose(true);
        }

        #endregion
    }
}
