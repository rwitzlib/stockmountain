"""One-time rescale of historical backtest CreditsUsed to the plan-16 credit unit.

Plan 16 phase 0 redefined 1 credit = 100 GB-seconds; older backtest records store raw
GB-seconds (100x too big for display). This divides CreditsUsed by 100 on every
SK=Context item and stamps CreditUnitVersion=2 so re-runs are no-ops.

IMPORTANT: run only AFTER the rescaled Backtest.Lambda (CreditMeter) is deployed —
until then the old worker still writes raw-scale values, and records created after this
script runs would stay raw.

Usage: python rescale_credits_used.py [--table stockmountain-dev-backtest-store] [--apply]
Without --apply it is a dry run.
"""

import argparse
from decimal import Decimal

import boto3


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--table", default="stockmountain-dev-backtest-store")
    parser.add_argument("--apply", action="store_true", help="write changes (default: dry run)")
    args = parser.parse_args()

    table = boto3.resource("dynamodb").Table(args.table)

    scanned = updated = skipped = 0
    kwargs = {"FilterExpression": "SK = :sk", "ExpressionAttributeValues": {":sk": "Context"}}
    while True:
        page = table.scan(**kwargs)
        for item in page["Items"]:
            scanned += 1
            if item.get("CreditUnitVersion") == 2:
                skipped += 1
                continue
            old = item.get("CreditsUsed", Decimal(0))
            new = old / Decimal(100)
            print(f"{item['PK']}: {old} -> {new}")
            if args.apply:
                table.update_item(
                    Key={"PK": item["PK"], "SK": "Context"},
                    UpdateExpression="SET CreditsUsed = :new, CreditUnitVersion = :v",
                    ConditionExpression="attribute_not_exists(CreditUnitVersion)",
                    ExpressionAttributeValues={":new": new, ":v": 2},
                )
            updated += 1
        if "LastEvaluatedKey" not in page:
            break
        kwargs["ExclusiveStartKey"] = page["LastEvaluatedKey"]

    mode = "updated" if args.apply else "would update"
    print(f"\nscanned {scanned} context records: {mode} {updated}, already converted {skipped}")


if __name__ == "__main__":
    main()
