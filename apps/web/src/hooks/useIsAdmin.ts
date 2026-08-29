import { useQuery } from '@tanstack/react-query';
import { useUser } from '@clerk/react';
import { userApi } from '../api/userApi';

/**
 * Whether the signed-in user is an admin. `isAdmin` stays false while loading
 * or signed out; use `isLoading` to avoid flashing gated UI at admins.
 */
export function useIsAdmin(): { isAdmin: boolean; isLoading: boolean } {
  const { user, isLoaded } = useUser();

  // Keyed by user (like billingSummary) so a sign-out/sign-in switch can
  // never gate on another user's cached details.
  const { data, isPending } = useQuery({
    queryKey: ['userDetails', user?.id],
    queryFn: () => userApi.getUser(user!.id),
    enabled: isLoaded && !!user?.id,
  });

  return {
    isAdmin: data?.isAdmin ?? false,
    isLoading: !isLoaded || (!!user?.id && isPending),
  };
}
