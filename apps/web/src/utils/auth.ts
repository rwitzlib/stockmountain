export const isAuthenticated = (): boolean => {
  const token = localStorage.getItem('accessToken');
  return !!token;
};

export const requireAuth = (): boolean => {
  const authenticated = isAuthenticated();
  if (!authenticated) {
    // Redirect to login or show login prompt
    return false;
  }
  return true;
}; 