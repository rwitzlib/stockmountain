# StockMountain

StockMountain is a stock market application where users query market data, evaluate trading ideas, and run automated trading logic managed by the system.

## Language

**User Account**:
The individual identity that owns Strategies, Backtest Runs, and Paper Bots in the first version.
_Avoid_: Customer, organization, workspace

**Strategy**:
A reusable, declarative definition of trading logic, including entry criteria, exit criteria, and position sizing rules. A **Strategy** can be evaluated historically or run automatically.
_Avoid_: Filter set, criteria, bot settings

**Strategy Version**:
A user-visible revision of a Strategy. Backtest Runs and Paper Bots are created from a specific Strategy Version.
_Avoid_: Strategy edit, draft overwrite

**Strategy Draft**:
The editable unpublished form of a Strategy. Publishing a Strategy Draft creates a Strategy Version.
_Avoid_: Unsaved version, mutable version

**Strategy Snapshot**:
The immutable Strategy definition captured for a Backtest Run or Paper Bot at creation or start time.
_Avoid_: Live strategy reference

**Strategy Filter**:
A declarative condition within a Strategy that is evaluated against one Timeframe. A Strategy may contain multiple Strategy Filters with different Timeframes.
_Avoid_: Expression timeframe, per-field timeframe

**Filter Group**:
A boolean combination of Strategy Filters or other Filter Groups. Filter Groups allow Strategies to express nested AND and OR logic.
_Avoid_: Filter list, criteria group

**Exit Rule**:
A declarative rule that determines when an open position should be closed. A **Strategy** may define one or more Exit Rules.
_Avoid_: Outcome, result model

**Exit Reason**:
The Exit Rule or execution condition that caused a Trade to close.
_Avoid_: Close reason, sell reason

**Exit Rule Priority**:
The deterministic order used to choose an Exit Reason when multiple Exit Rules trigger at the same evaluation time.
_Avoid_: Exit precedence, close order

**Timed Exit**:
An Exit Rule that closes a position after a defined duration or number of market data intervals.
_Avoid_: Hold version

**Conditional Exit**:
An Exit Rule that closes a position when a Filter Group becomes true.
_Avoid_: Indicator exit

**Stop Loss**:
An Exit Rule that closes a Trade when its loss reaches a configured percentage threshold.
_Avoid_: Loss filter

**Take Profit**:
An Exit Rule that closes a Trade when its gain reaches a configured percentage threshold.
_Avoid_: Profit filter

**Outcome Model**:
An analytical model used to evaluate what result was possible after a trade entry. An **Outcome Model** is not executable trading logic and must not be used as a live bot exit rule.
_Avoid_: Exit, high exit

**Best Case Outcome**:
An Outcome Model that measures the most favorable possible result after a trade entry within a defined evaluation window.
_Avoid_: High

**Signal**:
A point in market data where a Strategy condition becomes true. A **Signal** may or may not become a Trade.
_Avoid_: Alert, match

**Rejected Signal**:
A Signal that did not become a Trade because it failed a trading constraint.
_Avoid_: Ignored signal, skipped trade

**Trade**:
A position lifecycle created from a Signal after applying Strategy rules and trading constraints. A **Trade** includes an entry and an exit.
_Avoid_: Signal, order

**Open Trade Limit**:
A Portfolio rule that limits how many Trades may be open at the same time. In the first version, a Portfolio allows only one open Trade per symbol.
_Avoid_: Pyramiding, stacking

**Position Sizing Rule**:
The part of a Strategy that determines the intended size of a Trade.
_Avoid_: Allocation setting, share count setting

**Fixed Dollar Sizing**:
A Position Sizing Rule that targets a configured dollar amount for each Trade.
_Avoid_: Cash sizing

**Fixed Percent Sizing**:
A Position Sizing Rule that targets a configured percentage of Portfolio value for each Trade.
_Avoid_: Percent allocation

**Entry Fill**:
The assumed price and time where a Signal becomes an open Trade.
_Avoid_: Buy price, entry price assumption

**Portfolio**:
The capital and positions used when evaluating or running a Strategy. Backtest Runs use a shared Portfolio across the entire Universe by default.
_Avoid_: Account, wallet

**Portfolio Constraint**:
A run-level limit on how a Portfolio can be used, such as starting capital, maximum open positions, or maximum allocation.
_Avoid_: Strategy sizing, bot setting

**Long Trade**:
A Trade that opens by buying a security and closes by selling that security. StockMountain's first version supports Long Trades only.
_Avoid_: Short trade, margin trade

**Bar**:
A market data interval with open, high, low, close, and volume values. Strategies are evaluated against Bars in the first version.
_Avoid_: Candle, tick

**Normalized Bar**:
A Bar represented in StockMountain's canonical market data format, independent of the external data provider that supplied it. Each Normalized Bar includes a timestamp for the bar-period start in UTC, open, high, low, close, volume, volume-weighted average price, and transaction count. The first version persists Normalized Bars as its durable historical market data.
_Avoid_: Provider bar, raw tick

**Bar Series**:
The set of Normalized Bars for one Supported Security at one Timeframe and one price adjustment policy. Catalog metadata and stored bar files describe a Bar Series; individual Normalized Bars do not repeat Timeframe or adjustment policy.
_Avoid_: Dataset, aggregate request

**Split-Adjusted Bar**:
A Normalized Bar whose prices account for stock splits but not dividend adjustments. Split-Adjusted Bars are the default for Strategy evaluation and Chart Data in the first version.
_Avoid_: Raw bar, dividend-adjusted bar

**Completed Bar**:
A Normalized Bar whose Timeframe interval has closed. Completed Bars are the default input for Strategy evaluation.
_Avoid_: Closed candle

**In-Progress Bar**:
A Normalized Bar whose Timeframe interval is still open and may change before completion. A Strategy may explicitly allow In-Progress Bars for evaluation.
_Avoid_: Live candle, partial candle

**Timeframe**:
The interval length of a Bar Series, such as one minute or one day. Timeframe identifies which Bar Series is being read or written; it is not repeated on each Normalized Bar.
_Avoid_: Resolution, period

**Evaluation Timeframe**:
The Timeframe that determines when a Strategy is evaluated during a run. By default, a Strategy's Evaluation Timeframe is the fastest Timeframe used by its entry Strategy Filters.
_Avoid_: Primary timeframe, run timeframe

**Universe**:
The set of securities a Strategy is allowed to evaluate during a specific run. Every backtest and automated Strategy run has an explicit Universe.
_Avoid_: Ticker list, symbols filter, market scope

**Universe Snapshot**:
The immutable set of Supported Securities captured for a Backtest Run or Paper Bot at creation or start time.
_Avoid_: Live watchlist reference

**Watchlist**:
A User Account-owned reusable list of Supported Securities that can be used as a Universe source.
_Avoid_: Symbol list, favorites

**Supported Security**:
A US equity or ETF that StockMountain supports in the first version.
_Avoid_: Option, crypto, forex, future

**Market Session Scope**:
The market sessions included when evaluating a Strategy, such as regular market hours or extended hours. Market Session Scope is a Backtest Run or Paper Bot configuration applied when reading or evaluating a Bar Series; it is not part of Bar Series identity. The first version defaults to regular market hours.
_Avoid_: Hours filter, trading session

**Backtest Run**:
A historical evaluation of a Strategy against a Universe over a defined time range. A Backtest Run produces Signals, Trades, and performance results.
_Avoid_: Backtest, simulation

**Execution Assumptions**:
The configurable assumptions used when turning Signals into Trades, including Entry Fill, slippage, and fees.
_Avoid_: Backtest settings, fill settings

**Backtest Run Status**:
The lifecycle state of an asynchronous Backtest Run, such as waiting for data, queued, running, completed, failed, or canceled.
_Avoid_: Job state, task status

**Backtest Work Item**:
A partition of Backtest Run execution that can be processed by a Backtest Worker.
_Avoid_: Lambda task, worker job

**Backtest Worker**:
A compute process that consumes Backtest Work Items and produces partial Backtest Run results.
_Avoid_: Lambda, VPS worker

**Backtest Priority**:
The speed and cost tier selected for Backtest Work Item execution. Higher priority work may consume more credits and use faster compute.
_Avoid_: Queue name, worker type

**Paper Bot**:
An automated run of a Strategy against live market data that simulates trading without placing real broker orders. StockMountain's first version supports Paper Bots only.
_Avoid_: Trading bot, live bot, broker bot

**Paused Paper Bot**:
A Paper Bot that is not actively evaluating live market data. Paper Bots are paused when Membership becomes inactive.
_Avoid_: Stopped bot, deleted bot

**Corporate Action Hold**:
A Paused Paper Bot state used when a corporate action could affect an open simulated position and requires review.
_Avoid_: Split adjustment, automatic repair

**Paper Bot Portfolio**:
The isolated simulated Portfolio owned by one Paper Bot. In the first version, each Paper Bot has its own Paper Bot Portfolio.
_Avoid_: Shared bot account

**Live Feed Connector**:
The service that owns the external live market data connection and publishes live market data for StockMountain's internal consumers.
_Avoid_: WebSocket API, live backend

**Scheduled Ingestion**:
A planned import of market data intended to keep commonly used Normalized Bars available before users request them.
_Avoid_: Cron fetch, preload

**Backfill**:
An import of missing historical market data needed for a specific requested use, such as a Backtest Run.
_Avoid_: On-demand ingestion, data repair

**Backfill Job**:
A queued unit of Backfill work for one Bar Series over one contiguous date range. Enqueueing an identical pending or running Backfill Job returns the existing job instead of creating a duplicate.
_Avoid_: Ingest task, fetch job

**Backfill Job Status**:
The lifecycle state of a Backfill Job: pending, running, completed, or failed. A failed Backfill Job does not retry automatically; the same range may be enqueued again as a new job.
_Avoid_: Job state, task status

**Chart Data**:
Historical or live Normalized Bars presented to users in charts.
_Avoid_: Provider chart data

**Notification**:
An in-app message that informs a User Account about important StockMountain events such as Backtest Run completion or Paper Bot activity.
_Avoid_: Email, alert

**Notification Preference**:
A User Account or Paper Bot setting that controls which in-app Notifications should be created.
_Avoid_: Alert setting

**Internal Testing API**:
An authenticated backend API surface intended for StockMountain team development and app testing, not for external developer access or general end-user interaction. End users interact with StockMountain through the frontend.
_Avoid_: Public developer API, provider proxy

**Internal Testing API Key**:
A revocable credential used by StockMountain team members or test automation to access the Internal Testing API.
_Avoid_: User password, provider token

**Internal Testing API Key Scope**:
A coarse permission assigned to an Internal Testing API Key that limits which Internal Testing API capabilities it can access.
_Avoid_: Role, claim

**Usage Policy**:
The limits that control how much StockMountain capability a User Account may consume, such as Backtest Run concurrency, active Paper Bots, API rate, and chart subscriptions.
_Avoid_: Subscription plan, quota

**Membership**:
The recurring access level for a User Account that includes a defined amount of StockMountain capability, such as active Paper Bot capacity.
_Avoid_: Credit balance

**Inactive Membership**:
A User Account state where prior StockMountain data remains readable, but new Backtest Run execution and Paper Bot operation are not allowed.
_Avoid_: Locked account, deleted account

**Credit Grant**:
A Credit Ledger entry that adds Credits to a User Account, such as a monthly Membership allowance or an additional Credit purchase.
_Avoid_: Top-up, refill

**Monthly Credit Grant**:
A Credit Grant from Membership that expires at the end of the billing period.
_Avoid_: Rollover credits

**Purchased Credit Grant**:
A Credit Grant from an additional purchase that does not expire.
_Avoid_: Monthly credits

**Credit**:
A unit of StockMountain usage consumed by resource-intensive actions such as Backtest Run execution.
_Avoid_: Token, point

**Credit Ledger**:
The record of Credit grants and Credit consumption for a User Account.
_Avoid_: Balance field, billing log

**Credit Reservation**:
A temporary hold of estimated Credits for work that has been accepted but not yet settled.
_Avoid_: Pending charge, precharge

## Example Dialogue

**Developer**: Should the bot have its own buy rules?

**Domain Expert**: No, the bot runs a Strategy. The Strategy defines the buy and sell logic.

**Developer**: Who owns a Strategy?

**Domain Expert**: A User Account owns Strategies, Backtest Runs, and Paper Bots.

**Developer**: What happens if a Strategy changes after a Backtest Run is submitted?

**Domain Expert**: The Backtest Run uses its Strategy Snapshot and does not change.

**Developer**: Do Strategy edits overwrite prior behavior?

**Domain Expert**: No. Strategy edits create Strategy Versions.

**Developer**: Does every edit create a Strategy Version?

**Domain Expert**: No. Users edit a Strategy Draft, and publishing creates a Strategy Version.

**Developer**: Is the one-minute Timeframe attached only to `close` in `sma(9) > close [1m]`?

**Domain Expert**: No. The Timeframe applies to the whole Strategy Filter, so both `sma(9)` and `close` are evaluated from one-minute Bars.

**Developer**: Are multiple Strategy Filters always combined with AND?

**Domain Expert**: No. Strategies use Filter Groups so filters can be combined with nested AND and OR logic.

**Developer**: Can the same Strategy be used for backtesting?

**Domain Expert**: Yes, a Strategy should be reusable for both historical evaluation and automated execution.

**Developer**: Should a bot use the Best Case Outcome to exit?

**Domain Expert**: No. A Best Case Outcome is hindsight analysis. A bot needs an Exit Rule such as a Timed Exit or Conditional Exit.

**Developer**: If more than one Exit Rule could close a Trade, what happens?

**Domain Expert**: The first Exit Rule to trigger closes the Trade, and the Trade records that Exit Reason.

**Developer**: What if multiple Exit Rules trigger on the same Bar?

**Domain Expert**: Exit Rule Priority determines which Exit Reason closes the Trade.

**Developer**: Did the backtest create a trade every time the Strategy matched?

**Domain Expert**: No. The Strategy match is a Signal. A Trade is created only when the Signal passes the trading constraints.

**Developer**: Do we keep Signals that fail trading constraints?

**Domain Expert**: Yes. They are Rejected Signals and should include the reason they did not become Trades.

**Developer**: Can a Portfolio open multiple Trades for the same symbol?

**Domain Expert**: Not in the first version. A Portfolio allows only one open Trade per symbol.

**Developer**: When does a Signal become a Trade in a Backtest Run?

**Domain Expert**: By default, the Entry Fill occurs at the next Bar's open.

**Developer**: Are slippage and fees part of the Strategy?

**Domain Expert**: No. They are Execution Assumptions for the run.

**Developer**: Does each symbol in a Backtest Run get its own capital?

**Domain Expert**: No. A Backtest Run uses a shared Portfolio across its Universe by default.

**Developer**: Is position sizing part of the run or the Strategy?

**Domain Expert**: The Strategy owns the Position Sizing Rule. The run owns Portfolio Constraints.

**Developer**: Can a Strategy open short positions?

**Domain Expert**: Not in the first version. The first version supports Long Trades only.

**Developer**: Should a Strategy evaluate every raw market tick?

**Domain Expert**: No. Strategies evaluate Bars, and each Bar has a Timeframe.

**Developer**: Can a Strategy evaluate an interval before the Bar has completed?

**Domain Expert**: Only if the Strategy explicitly allows In-Progress Bars. Otherwise Strategy evaluation uses Completed Bars.

**Developer**: If a Strategy has one-minute and daily Strategy Filters, when is it evaluated?

**Domain Expert**: By default, it is evaluated on the fastest filter Timeframe, while slower filters use their latest available values.

**Developer**: Is the Universe part of the Strategy?

**Domain Expert**: No. The Strategy defines reusable trading logic, while the run supplies the Universe.

**Developer**: Can a user reuse the same group of symbols across runs?

**Domain Expert**: Yes. A Watchlist can be used as a Universe source.

**Developer**: What happens if a Watchlist changes after a Backtest Run is submitted?

**Domain Expert**: The Backtest Run uses its Universe Snapshot and does not change.

**Developer**: If a backtest does not specify symbols, should it scan everything?

**Domain Expert**: No. Every backtest has an explicit Universe, even when that Universe means all supported securities.

**Developer**: Are pre-market and after-hours Bars included by default?

**Domain Expert**: No. The default Market Session Scope is regular market hours.

**Developer**: Did the user create a new Backtest?

**Domain Expert**: Say Backtest Run when referring to one historical execution of a Strategy.

**Developer**: Is a Backtest Work Item tied to Lambda?

**Domain Expert**: No. A Backtest Worker can process Backtest Work Items from Lambda, a VPS, or another runtime.

**Developer**: Does high-priority backtesting mean different results?

**Domain Expert**: No. Backtest Priority changes execution speed and cost, not Backtest Run semantics.

**Developer**: Will the first version place real trades through a broker?

**Domain Expert**: No. The first version supports Paper Bots only.

**Developer**: Does a Paper Bot automatically use the newest Strategy Version?

**Domain Expert**: No. A Paper Bot stays pinned to its selected Strategy Version until explicitly changed.

**Developer**: Do multiple Paper Bots share simulated capital?

**Domain Expert**: No. Each Paper Bot owns its own Paper Bot Portfolio in the first version.

**Developer**: What happens to Paper Bots when Membership becomes inactive?

**Domain Expert**: They are paused, with any billing grace period handled before Membership becomes inactive.

**Developer**: What happens if a split affects an open Paper Bot position?

**Domain Expert**: The affected Paper Bot enters Corporate Action Hold for review.

**Developer**: Should the Backend API connect directly to the external live market feed?

**Domain Expert**: No. The Live Feed Connector owns the external live market data connection.

**Developer**: What happens if a Backtest Run needs data we have not stored yet?

**Domain Expert**: The system performs a Backfill for the missing historical market data.

**Developer**: Does Backfill consume the requesting User Account's Credits?

**Domain Expert**: No. Credits are consumed by Backtest Run execution, not by Backfill.

**Developer**: Is a Backfill Job tied to one Backtest Run?

**Domain Expert**: No. A Backfill Job is platform work for one Bar Series and date range. A Backtest Run waits until catalog coverage is satisfied, not until a specific job completes.

**Developer**: Should charts use market data directly from the external provider?

**Domain Expert**: No. Charts use Chart Data from StockMountain's Normalized Bars.

**Developer**: How are users informed about Backtest Run completion or Paper Bot activity in V1?

**Domain Expert**: StockMountain uses in-app Notifications in the first version.

**Developer**: Should every Paper Bot Trade create a Notification?

**Domain Expert**: Paper Bot Notifications are controlled by Notification Preferences.


**Developer**: Do end users call the Backend API directly?

**Domain Expert**: No. End users use the frontend, while the StockMountain team may use the Internal Testing API.

**Developer**: Should scripts use Clerk browser sessions?

**Domain Expert**: Test automation may use Internal Testing API Keys.

**Developer**: Can an authenticated User Account run unlimited Paper Bots and Backtest Runs?

**Domain Expert**: No. Usage Policy limits resource consumption even before paid billing exists.

**Developer**: Do Paper Bots consume Credits while they run?

**Domain Expert**: No. Membership covers a defined amount of Paper Bot capacity.

**Developer**: Does Paper Bot capacity limit all created bots?

**Domain Expert**: No. Membership limits active Paper Bots, not paused or historical Paper Bot records.

**Developer**: Can purchased Credits be used without an active Membership?

**Domain Expert**: No. Active Membership is required for Backtest Run execution and Paper Bots.

**Developer**: Does canceling Membership hide old results?

**Domain Expert**: No. Inactive Membership keeps prior data readable but prevents new execution.

**Developer**: Where do Backtest Run Credits come from?

**Domain Expert**: Membership grants a recurring Credit allowance, and users may purchase additional Credits.

**Developer**: How do we account for expensive Backtest Run execution?

**Domain Expert**: Resource-intensive actions consume Credits recorded in the User Account's Credit Ledger.

**Developer**: Are Backtest Run Credits charged immediately?

**Domain Expert**: Credits are reserved when the Backtest Run is accepted and settled when execution finishes.
