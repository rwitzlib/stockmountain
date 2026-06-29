import { useState, useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { Button } from '../components/ui/button';
import { Input } from '../components/ui/input';
import { LogOut, Save, User } from 'lucide-react';
// import Navbar from '../components/Navbar';
import { jwtDecode } from 'jwt-decode';

interface JWTPayload {
  sub: string;
  properties: {
    id: number;
    username: string;
    email: string | null;
    avatar: string | null;
  }
}

const Profile = () => {
  const navigate = useNavigate();
  const [username, setUsername] = useState('');
  const [isLoading, setIsLoading] = useState(false);

  useEffect(() => {
    const token = localStorage.getItem('accessToken');
    if (!token) {
      navigate('/');
      return;
    }

    const decoded = jwtDecode<JWTPayload>(token);
    if (decoded.properties.email) {
      setUsername(decoded.properties.email);
    }
  }, [navigate]);

  const handleLogout = () => {
    localStorage.removeItem('accessToken');
    localStorage.removeItem('refresh');
    navigate('/');
  };

  const handleUpdateUsername = async () => {
    setIsLoading(true);
    try {
      const response = await fetch('https://auth.stockmountain.io/api/v1/users/me', {
        method: 'PATCH',
        headers: {
          'Content-Type': 'application/json',
          'Authorization': `Bearer ${localStorage.getItem('accessToken')}`
        },
        body: JSON.stringify({
          username: username
        })
      });

      if (!response.ok) {
        throw new Error('Failed to update username');
      }

      // Refresh the page to update the navbar
      window.location.reload();
    } catch (error) {
      console.error('Error updating username:', error);
    } finally {
      setIsLoading(false);
    }
  };

  return (
    <div className="min-h-screen bg-background">
      {/* <Navbar /> */}
      <div className="p-4 md:p-8 pt-20 md:pt-8">
        <div className="max-w-2xl mx-auto space-y-8">
          <div>
            <h1 className="text-2xl font-bold gradient-heading">Profile Settings</h1>
            <p className="text-muted-foreground mt-2">Manage your account settings and preferences</p>
          </div>

          <div className="space-y-6 p-6 bg-card/50 backdrop-blur-sm rounded-xl border">
            <div className="space-y-4">
              <div className="space-y-2">
                <label htmlFor="username" className="text-sm font-medium">
                  Username
                </label>
                <div className="flex gap-2">
                  <Input
                    id="username"
                    value={username}
                    onChange={(e) => setUsername(e.target.value)}
                    className="flex-1"
                  />
                  <Button 
                    onClick={handleUpdateUsername}
                    disabled={isLoading}
                    className="gap-2"
                  >
                    <Save className="w-4 h-4" />
                    Save
                  </Button>
                </div>
              </div>
            </div>

            <div className="pt-4 border-t">
              <Button 
                variant="destructive" 
                onClick={handleLogout}
                className="gap-2"
              >
                <LogOut className="w-4 h-4" />
                Logout
              </Button>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default Profile; 