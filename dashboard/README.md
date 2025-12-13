# Verbex Dashboard

A React-based web dashboard for managing Verbex inverted indices.

## Screenshots

<div align="center">
  <img src="../assets/screenshot1.png" alt="Dashboard Overview" width="800">
  <p><em>Dashboard Overview</em></p>
</div>

<div align="center">
  <img src="../assets/screenshot2.png" alt="Index Management" width="800">
  <p><em>Index Management</em></p>
</div>

<div align="center">
  <img src="../assets/screenshot3.png" alt="Search Interface" width="800">
  <p><em>Search Interface</em></p>
</div>

## Features

- **Index Management**: Create, view, and delete indices with configurable storage modes and text processing options
- **Document Operations**: Add, view, and delete documents from indices
- **Search**: Full-text search across indexed documents with configurable result limits
- **Dark/Light Theme**: Toggle between light and dark modes
- **Persistent Sessions**: Authentication and preferences saved to localStorage

## Prerequisites

- Node.js 18+ (recommended: 22)
- npm or yarn
- Verbex Server running (default: http://localhost:8080)

## Getting Started

### Development

```bash
# Install dependencies
npm install

# Start development server (port 8200)
npm run dev
```

### Production Build

```bash
# Build for production
npm run build

# Preview production build
npm run preview
```

### Docker

```bash
# Build Docker image
docker build -t verbex-dashboard .

# Run container
docker run -p 8200:8200 verbex-dashboard
```

## Configuration

Environment variables can be set to configure default values:

- `VERBEX_SERVER_URL`: Default server URL (default: `http://localhost:8080`)
- `VERBEX_API_KEY`: Default API key (default: `verbexadmin`)

## Project Structure

```
dashboard/
├── src/
│   ├── components/       # React components
│   │   ├── Dashboard.jsx     # Main layout container
│   │   ├── Login.jsx         # Authentication page
│   │   ├── Topbar.jsx        # Header navigation
│   │   ├── Sidebar.jsx       # Left navigation panel
│   │   ├── Workspace.jsx     # Main content router
│   │   ├── IndicesView.jsx   # Index management
│   │   ├── SearchView.jsx    # Search interface
│   │   ├── DocumentsView.jsx # Document management
│   │   ├── IndexForm.jsx     # Index creation form
│   │   └── Modal.jsx         # Reusable modal component
│   ├── context/
│   │   └── AuthContext.jsx   # Authentication state
│   ├── utils/
│   │   └── api.js            # Verbex API client
│   ├── App.jsx               # Root component with routing
│   ├── main.jsx              # Entry point
│   └── index.css             # Global styles and themes
├── index.html
├── package.json
├── vite.config.js
└── Dockerfile
```

## API Endpoints Used

The dashboard communicates with the following Verbex Server endpoints:

### Authentication
- `POST /v1.0/auth/login` - Login with credentials
- `GET /v1.0/auth/validate` - Validate token

### Indices
- `GET /v1.0/indices` - List all indices
- `GET /v1.0/indices/{id}` - Get index details
- `POST /v1.0/indices` - Create new index
- `DELETE /v1.0/indices/{id}` - Delete index

### Documents
- `GET /v1.0/indices/{id}/documents` - List documents
- `GET /v1.0/indices/{id}/documents/{docId}` - Get document
- `POST /v1.0/indices/{id}/documents` - Add document
- `DELETE /v1.0/indices/{id}/documents/{docId}` - Delete document

### Search
- `POST /v1.0/indices/{id}/search` - Search documents

## License

MIT
