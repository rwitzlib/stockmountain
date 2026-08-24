import { authFetch } from './authToken';
import { API_BASE_URL as BASE_URL } from './apiConfig';


export interface UserDetails {
	id: string;
	avatarUrl: string | null;
	credits: number;
	maxCredits: number;
	isPublic: boolean;
	role: 'Free' | 'Pro' | 'Premium';
	purchasedCredits?: number;
	isAdmin: boolean;
}

export const userApi = {
	getUser: async (userId: string): Promise<UserDetails> => {
		const response = await authFetch(`${BASE_URL}/user/${userId}`, {
			method: 'GET'
		});

		if (!response.ok) {
			throw new Error('Failed to fetch user details');
		}

		return await response.json();
	}
};
