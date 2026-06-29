const BASE_URL = 'https://api.stockmountain.io/api';

const getAuthHeaders = () => ({
	'Content-Type': 'application/json',
	'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
});

export interface UserDetails {
	id: number;
	avatarurl: string | null;
	credits: number;
	ispublic: boolean;
	role: string;
}

export const userApi = {
	getUser: async (userId: number | string): Promise<UserDetails> => {
		const response = await fetch(`${BASE_URL}/user/${userId}`, {
			method: 'GET',
			headers: getAuthHeaders()
		});

		if (!response.ok) {
			throw new Error('Failed to fetch user details');
		}

		return await response.json();
	}
};


