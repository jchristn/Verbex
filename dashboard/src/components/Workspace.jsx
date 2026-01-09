import IndicesView from './IndicesView';
import SearchView from './SearchView';
import DocumentsView from './DocumentsView';
import TenantsView from './TenantsView';
import UsersView from './UsersView';
import CredentialsView from './CredentialsView';
import './Workspace.css';

function Workspace({
  activeView,
  selectedIndex,
  indices,
  isLoading,
  onRefresh,
  onIndexSelect,
  onIndexSelectAndNavigate,
  tenants,
  selectedTenant,
  onTenantSelect,
  onTenantSelectAndNavigate
}) {
  const renderContent = () => {
    switch (activeView) {
      case 'indices':
        return (
          <IndicesView
            indices={indices}
            isLoading={isLoading}
            onRefresh={onRefresh}
            onIndexSelectAndNavigate={onIndexSelectAndNavigate}
            tenants={tenants}
          />
        );
      case 'search':
        return (
          <SearchView
            selectedIndex={selectedIndex}
            indices={indices}
            onIndexSelect={onIndexSelect}
          />
        );
      case 'documents':
        return (
          <DocumentsView
            selectedIndex={selectedIndex}
            indices={indices}
            onRefresh={onRefresh}
            onIndexSelect={onIndexSelect}
          />
        );
      case 'tenants':
        return (
          <TenantsView
            onTenantSelect={onTenantSelectAndNavigate}
          />
        );
      case 'users':
        return (
          <UsersView
            selectedTenant={selectedTenant}
            tenants={tenants}
            onTenantSelect={onTenantSelect}
          />
        );
      case 'credentials':
        return (
          <CredentialsView
            selectedTenant={selectedTenant}
            tenants={tenants}
            onTenantSelect={onTenantSelect}
          />
        );
      default:
        return <div className="workspace-empty">Select a view from the sidebar</div>;
    }
  };

  return (
    <main className="workspace">
      {renderContent()}
    </main>
  );
}

export default Workspace;
