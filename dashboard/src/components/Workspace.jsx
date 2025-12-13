import IndicesView from './IndicesView';
import SearchView from './SearchView';
import DocumentsView from './DocumentsView';
import './Workspace.css';

function Workspace({ activeView, selectedIndex, indices, isLoading, onRefresh, onIndexSelect, onIndexSelectAndNavigate }) {
  const renderContent = () => {
    switch (activeView) {
      case 'indices':
        return (
          <IndicesView
            indices={indices}
            isLoading={isLoading}
            onRefresh={onRefresh}
            onIndexSelectAndNavigate={onIndexSelectAndNavigate}
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
