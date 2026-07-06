import { useEffect } from 'react';
import { useNavigate } from 'react-router-dom';
import { useClerk, useUser, UserProfile } from '@clerk/react';
import { Button } from '../components/ui/button';
import { LogOut } from 'lucide-react';

const Profile = () => {
  const navigate = useNavigate();
  const { isLoaded, isSignedIn } = useUser();
  const { signOut } = useClerk();

  useEffect(() => {
    if (isLoaded && !isSignedIn) {
      navigate('/');
    }
  }, [isLoaded, isSignedIn, navigate]);

  const handleLogout = async () => {
    await signOut();
    navigate('/');
  };

  if (!isLoaded || !isSignedIn) {
    return null;
  }

  return (
    <div className="min-h-screen bg-background">
      <div className="p-4 md:p-8 pt-20 md:pt-8">
        <div className="max-w-2xl mx-auto space-y-8">
          <div>
            <h1 className="text-2xl font-bold gradient-heading">Profile Settings</h1>
            <p className="text-muted-foreground mt-2">Manage your account settings and preferences</p>
          </div>

          <div className="space-y-6 p-6 bg-card/50 backdrop-blur-sm rounded-xl border">
            <UserProfile routing="hash" />

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
