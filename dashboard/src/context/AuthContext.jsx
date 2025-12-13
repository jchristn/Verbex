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

  // Load saved credentials and theme on mount
  useEffect(() => {
    const savedUrl = localStorage.getItem('verbex_server_url');
    const savedToken = localStorage.getItem('verbex_token');
    const savedTheme = localStorage.getItem('verbex_theme') || 'light';

    setTheme(savedTheme);
    document.body.setAttribute('data-theme', savedTheme);

    if (savedUrl && savedToken) {
      const client = new ApiClient(savedUrl, savedToken);
      client.validateToken()
        .then(() => {
          setServerUrl(savedUrl);
          setToken(savedToken);
          setApiClient(client);
          setIsAuthenticated(true);
        })
        .catch(() => {
          // Token invalid, clear storage
          localStorage.removeItem('verbex_server_url');
          localStorage.removeItem('verbex_token');
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

    // If credentials object provided, login first
    if (typeof tokenOrCredentials === 'object') {
      const tempClient = new ApiClient(url, null);
      const response = await tempClient.login(tokenOrCredentials.username, tokenOrCredentials.password);
      finalToken = response.data.token;
    }

    const client = new ApiClient(url, finalToken);
    await client.validateToken();

    localStorage.setItem('verbex_server_url', url);
    localStorage.setItem('verbex_token', finalToken);

    setServerUrl(url);
    setToken(finalToken);
    setApiClient(client);
    setIsAuthenticated(true);
  };

  const logout = () => {
    localStorage.removeItem('verbex_server_url');
    localStorage.removeItem('verbex_token');
    setServerUrl('');
    setToken('');
    setApiClient(null);
    setIsAuthenticated(false);
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
