use FifthPress
go

with cte as
(
select Id from Quotes where cast(JobNumber as int) >= 19626
)

delete from ItemizedQuotePricing where PricingRecord in 
(select Id from QuotePricing where QuoteItemId in (select Id from QuoteItems where Quote in (select Id from cte)))
go

with cte as
(
select Id from Quotes where cast(JobNumber as int) >= 19626
)
delete from QuotePricing
where QuoteItemId in (select Id from QuoteItems where Quote in (select Id from cte))
go

with cte as
(
select Id from Quotes where cast(JobNumber as int) >= 19626
)
delete from QuoteItemServices where QuoteItemId in (select Id from QuoteItems where Quote in (select Id from cte))
go

with cte as
(
select Id from Quotes where cast(JobNumber as int) >= 19626
)
delete from QuoteItems where Quote in (select Id from cte)
go

with cte as
(
select Id from Quotes where cast(JobNumber as int) >= 19626
)
delete from Quotes where Id in (select Id from cte)
go