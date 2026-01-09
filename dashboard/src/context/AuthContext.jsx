import { createContext, useContext, useState, useEffect } from 'react';
import ApiClient from '../utils/api';

const AuthContext = createContext(null);

export function AuthProvider({ children }) {
  const [isAuthenticated, setIsAuthenticated] = useState(false);
  const [apiClient, setApiClient] = useState(null);
  const [serverUrl, setServerUrl] = useState('');
  const [token, setToken] = useState('');
  const [theme, setTheme] = useState('light');
  const [isLoading, setIsLoading] = useState(true);
  const [userInfo, setUserInfo] = useState(null);

  // Load saved credentials and theme on mount
  useEffect(() => {
    const savedUrl = localStorage.getItem('verbex_server_url');
    const savedToken = localStorage.getItem('verbex_token');
    const savedTheme = localStorage.getItem('verbex_theme') || 'light';
    const savedUserInfo = localStorage.getItem('verbex_user_info');

    setTheme(savedTheme);
    document.body.setAttribute('data-theme', savedTheme);

    if (savedUrl && savedToken) {
      const client = new ApiClient(savedUrl, savedToken);
      client.validateToken()
        .then((response) => {
          setServerUrl(savedUrl);
          setToken(savedToken);
          setApiClient(client);
          setIsAuthenticated(true);

          // Use auth context from validate response, fall back to saved userInfo
          if (response.data) {
            const authContext = {
              email: response.data.email,
              tenantId: response.data.tenantId,
              userId: response.data.userId,
              isGlobalAdmin: response.data.isGlobalAdmin || false,
              isAdmin: response.data.isGlobalAdmin || response.data.isTenantAdmin || false,
              isTenantAdmin: response.data.isTenantAdmin || false
            };
            setUserInfo(authContext);
            localStorage.setItem('verbex_user_info', JSON.stringify(authContext));
          } else if (savedUserInfo) {
            try {
              setUserInfo(JSON.parse(savedUserInfo));
            } catch (e) {
              // ignore parse errors
            }
          }
        })
        .catch(() => {
          // Token invalid, clear storage
          localStorage.removeItem('verbex_server_url');
          localStorage.removeItem('verbex_token');
          localStorage.removeItem('verbex_user_info');
        })
        .finally(() => {
          setIsLoading(false);
        });
    } else {
      setIsLoading(false);
    }
  }, []);

  const login = async (url, tokenOrCredentials) => {
    let finalToken = tokenOrCredentials;
    let loginUserInfo = null;

    // If credentials object provided, login first
    if (typeof tokenOrCredentials === 'object') {
      const tempClient = new ApiClient(url, null);
      const response = await tempClient.login(
        tokenOrCredentials.username,
        tokenOrCredentials.password,
        tokenOrCredentials.tenantId
      );
      finalToken = response.data.token;
      loginUserInfo = {
        email: response.data.email,
        firstName: response.data.firstName,
        lastName: response.data.lastName,
        tenantId: response.data.tenantId,
        isGlobalAdmin: response.data.isGlobalAdmin || false,
        isAdmin: response.data.isAdmin || false
      };
    }

    const client = new ApiClient(url, finalToken);
    const validateResponse = await client.validateToken();

    // If no loginUserInfo from credentials login, build it from validate response
    if (!loginUserInfo && validateResponse.data) {
      loginUserInfo = {
        email: validateResponse.data.email,
        tenantId: validateResponse.data.tenantId,
        userId: validateResponse.data.userId,
        isGlobalAdmin: validateResponse.data.isGlobalAdmin || false,
        isAdmin: validateResponse.data.isGlobalAdmin || validateResponse.data.isTenantAdmin || false,
        isTenantAdmin: validateResponse.data.isTenantAdmin || false
      };
    }

    localStorage.setItem('verbex_server_url', url);
    localStorage.setItem('verbex_token', finalToken);
    if (loginUserInfo) {
      localStorage.setItem('verbex_user_info', JSON.stringify(loginUserInfo));
    }

    setServerUrl(url);
    setToken(finalToken);
    setApiClient(client);
    setIsAuthenticated(true);
    setUserInfo(loginUserInfo);
  };

  const logout = () => {
    localStorage.removeItem('verbex_server_url');
    localStorage.removeItem('verbex_token');
    localStorage.removeItem('verbex_user_info');
    setServerUrl('');
    setToken('');
    setApiClient(null);
    setIsAuthenticated(false);
    setUserInfo(null);
  };

  const toggleTheme = () => {
    const newTheme = theme === 'light' ? 'dark' : 'light';
    setTheme(newTheme);
    localStorage.setItem('verbex_theme', newTheme);
    document.body.setAttribute('data-theme', newTheme);
  };

  const value = {
    isAuthenticated,
    isLoading,
    apiClient,
    serverUrl,
    token,
    theme,
    userInfo,
    login,
    logout,
    toggleTheme
  };

  return (
    <AuthContext.Provider value={value}>
      {children}
    </AuthContext.Provider>
  );
}

export function useAuth() {
  const context = useContext(AuthContext);
  if (!context) {
    throw new Error('useAuth must be used within an AuthProvider');
  }
  return context;
}

export default AuthContext;
