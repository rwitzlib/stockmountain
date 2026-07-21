import { useEffect, useState } from 'react';
import { CheckCircle, XCircle, AlertTriangle, Loader2 } from 'lucide-react';
import { API_ORIGIN } from '../../api/apiConfig';

type ApiStatusType = 'loading' | 'healthy' | 'unhealthy' | 'unknown';

interface ApiStatusInfo {
  status: ApiStatusType;
  label: string;
  color: string;
  bgColor: string;
  borderColor: string;
  icon: React.ComponentType<any>;
  description: string;
}

export function ApiStatus() {
  const [apiStatus, setApiStatus] = useState<ApiStatusType>('loading');

  const checkApiHealth = async () => {
    try {
      const response = await fetch(`${API_ORIGIN}/health`, {
        method: 'GET',
        headers: {
          'Content-Type': 'application/json',
        },
        // Short timeout for quick status check
        signal: AbortSignal.timeout(5000),
      });

      if (response.ok) {
        setApiStatus('healthy');
      } else {
        setApiStatus('unhealthy');
      }
    } catch (error) {
      console.warn('API health check failed:', error);
      setApiStatus('unhealthy');
    }
  };

  useEffect(() => {
    // Initial check
    checkApiHealth();

    // Check every 30 seconds
    const interval = setInterval(checkApiHealth, 30000);

    return () => clearInterval(interval);
  }, []);

  const getStatusInfo = (): ApiStatusInfo => {
    switch (apiStatus) {
      case 'loading':
        return {
          status: 'loading',
          label: 'CHECKING',
          color: 'text-yellow-600 dark:text-yellow-400',
          bgColor: 'bg-yellow-100/50 dark:bg-yellow-950/30',
          borderColor: 'border-yellow-300 dark:border-yellow-800',
          icon: Loader2,
          description: 'API Status'
        };
      case 'healthy':
        return {
          status: 'healthy',
          label: 'ONLINE',
          color: 'text-green-600 dark:text-green-400',
          bgColor: 'bg-green-100/50 dark:bg-green-950/30',
          borderColor: 'border-green-300 dark:border-green-800',
          icon: CheckCircle,
          description: 'API Status'
        };
      case 'unhealthy':
        return {
          status: 'unhealthy',
          label: 'OFFLINE',
          color: 'text-red-600 dark:text-red-400',
          bgColor: 'bg-red-100/50 dark:bg-red-950/30',
          borderColor: 'border-red-300 dark:border-red-800',
          icon: XCircle,
          description: 'API Status'
        };
      default:
        return {
          status: 'unknown',
          label: 'UNKNOWN',
          color: 'text-muted-foreground',
          bgColor: 'bg-muted/50',
          borderColor: 'border-border',
          icon: AlertTriangle,
          description: 'API Status'
        };
    }
  };

  const statusInfo = getStatusInfo();
  const Icon = statusInfo.icon;

  return (
    <div className={`flex items-center gap-2 rounded-full ${statusInfo.bgColor} border ${statusInfo.borderColor} px-3 py-1.5 transition-colors duration-300`}>
      <Icon className={`w-3 h-3 ${statusInfo.color} ${apiStatus === 'loading' ? 'animate-spin' : ''}`} />
      <span className={`text-xs font-medium ${statusInfo.color}`}>
        API {statusInfo.label}
      </span>
    </div>
  );
}
