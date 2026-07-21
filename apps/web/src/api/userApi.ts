import { getAuthHeaders } from './authToken';
import { API_BASE_URL as BASE_URL } from './apiConfig';


export interface UserDetails {
	id: string;
	avatarUrl: string | null;
	credits: number;
	isPublic: boolean;
	role: 'Basic' | 'Advanced' | 'Premium';
	isAdmin: boolean;
}

export const userApi = {
	getUser: async (userId: string): Promise<UserDetails> => {
		const response = await fetch(`${BASE_URL}/user/${userId}`, {
			method: 'GET',
			headers: await getAuthHeaders()
		});

		if (!response.ok) {
			throw new Error('Failed to fetch user details');
		}

		return await response.json();
	}
};
