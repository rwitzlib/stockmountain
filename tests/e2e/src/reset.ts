/**
 * Test-state reset against the dev user-store (plan 16 phase 5). Uses ambient
 * AWS credentials (env vars locally, OIDC role in CI). Only ever touches the
 * fixed test users' rows; UpdateItem creates the row if it doesn't exist yet
 * and preserves unrelated attributes (Tokens, AvatarUrl, ...).
 */
import {
  DeleteItemCommand,
  DynamoDBClient,
  UpdateItemCommand,
} from '@aws-sdk/client-dynamodb';
import { assertDevSafety, env } from './env';

const client = new DynamoDBClient({ region: env.awsRegion });

export interface UserRowState {
  role: 'Free' | 'Pro' | 'Premium';
  credits: number;
  maxCredits: number;
  purchasedCredits: number;
}

/**
 * Force a user row into a known billing state and drop any Stripe linkage so
 * the next checkout starts from a clean slate.
 *
 * The non-billing attributes are backfilled with provisioning-shaped defaults
 * via if_not_exists: UserRepository.Get reads AvatarUrl/IsPublic
 * unconditionally, so a row this reset creates from scratch (the fixed test
 * users never sign up through the app) must be complete or every authed API
 * call 500s. Existing values are never overwritten.
 */
export async function resetUserRow(userId: string, state: UserRowState): Promise<void> {
  assertDevSafety();
  await client.send(
    new UpdateItemCommand({
      TableName: env.userStoreTable,
      Key: { Id: { S: userId } },
      UpdateExpression:
        'SET #role = :role, #credits = :credits, #maxCredits = :maxCredits, ' +
        '#purchasedCredits = :purchasedCredits, ' +
        '#isAdmin = if_not_exists(#isAdmin, :isAdmin), ' +
        '#avatarUrl = if_not_exists(#avatarUrl, :avatarUrl), ' +
        '#isPublic = if_not_exists(#isPublic, :isPublic), ' +
        '#tokens = if_not_exists(#tokens, :tokens) ' +
        'REMOVE #stripeCustomerId, #subscriptionStatus',
      ExpressionAttributeNames: {
        '#role': 'Role',
        '#credits': 'Credits',
        '#maxCredits': 'MaxCredits',
        '#purchasedCredits': 'PurchasedCredits',
        '#isAdmin': 'IsAdmin',
        '#avatarUrl': 'AvatarUrl',
        '#isPublic': 'IsPublic',
        '#tokens': 'Tokens',
        '#stripeCustomerId': 'StripeCustomerId',
        '#subscriptionStatus': 'SubscriptionStatus',
      },
      ExpressionAttributeValues: {
        ':role': { S: state.role },
        ':credits': { N: state.credits.toString() },
        ':maxCredits': { N: state.maxCredits.toString() },
        ':purchasedCredits': { N: state.purchasedCredits.toString() },
        ':isAdmin': { BOOL: false },
        ':avatarUrl': { S: '' },
        // Stored as the .NET bool.ToString() form the repository round-trips.
        ':isPublic': { S: 'False' },
        ':tokens': { M: {} },
      },
    })
  );
}

/** Remove a user row entirely (cleanup for the throwaway signup user). */
export async function deleteUserRow(userId: string): Promise<void> {
  assertDevSafety();
  await client.send(
    new DeleteItemCommand({
      TableName: env.userStoreTable,
      Key: { Id: { S: userId } },
    })
  );
}
