import { useMemo } from 'react';
import { useAuth } from '../context/AuthContext';
import './ObservabilityView.css';

function ObservabilityView() {
  const { serverUrl } = useAuth();

  const services = useMemo(() => {
    const grafanaUrl = window.__ENV__?.VERBEX_GRAFANA_URL || 'http://localhost:3000';
    const prometheusUrl = window.__ENV__?.VERBEX_PROMETHEUS_URL || 'http://localhost:9090';
    const tempoUrl = window.__ENV__?.VERBEX_TEMPO_URL || 'http://localhost:3200';
    const lokiUrl = window.__ENV__?.VERBEX_LOKI_URL || 'http://localhost:3100';
    const metricsUrl = window.__ENV__?.VERBEX_METRICS_URL || 'http://localhost:9464/metrics';
    const swaggerUrl = serverUrl ? `${serverUrl.replace(/\/$/, '')}/swagger` : '';

    return [
      {
        id: 'grafana',
        name: 'Grafana',
        description: 'Dashboards, metrics, traces and logs visualization.',
        credentials: 'admin / admin',
        url: grafanaUrl
      },
      {
        id: 'prometheus',
        name: 'Prometheus',
        description: 'Metrics storage and querying (PromQL).',
        credentials: 'None',
        url: prometheusUrl
      },
      {
        id: 'tempo',
        name: 'Tempo',
        description: 'Distributed tracing backend (view traces via Grafana Explore).',
        credentials: 'None',
        url: tempoUrl
      },
      {
        id: 'loki',
        name: 'Loki',
        description: 'Log aggregation backend (view logs via Grafana Explore).',
        credentials: 'None',
        url: lokiUrl
      },
      {
        id: 'swagger',
        name: 'Verbex API (Swagger)',
        description: 'Interactive REST API documentation.',
        credentials: 'Bearer token',
        url: swaggerUrl
      },
      {
        id: 'metrics',
        name: 'Prometheus metrics endpoint',
        description: 'Raw in-process Prometheus scrape endpoint exposed by the Verbex server.',
        credentials: 'None',
        url: metricsUrl
      }
    ];
  }, [serverUrl]);

  return (
    <div className="observability-view">
      <div className="workspace-header">
        <div className="workspace-title">
          <h2>Observability</h2>
          <p className="workspace-subtitle">Jump to the observability stack services for metrics, traces, logs, and API documentation. Each link opens in a new browser tab.</p>
        </div>
      </div>

      <div className="observability-note">
        These links point to the observability stack defined in <code>docker/compose.yaml</code>. Service URLs are configurable at runtime via the dashboard's env-config (<code>window.__ENV__</code>).
      </div>

      <div className="observability-grid">
        {services.map((service) => (
          <div key={service.id} className="workspace-card observability-card">
            <div className="workspace-card-header">
              <h3>{service.name}</h3>
            </div>
            <div className="workspace-card-body observability-card-body">
              <p className="observability-description">{service.description}</p>
              <div className="observability-meta">
                <div className="observability-meta-row">
                  <span className="observability-meta-label">Default credentials</span>
                  <span className="observability-meta-value">{service.credentials}</span>
                </div>
                <div className="observability-meta-row">
                  <span className="observability-meta-label">URL</span>
                  <code className="observability-url">{service.url || 'n/a'}</code>
                </div>
              </div>
              <div className="observability-card-actions">
                {service.url ? (
                  <a
                    className="btn btn-primary"
                    href={service.url}
                    target="_blank"
                    rel="noopener noreferrer"
                  >
                    Open
                  </a>
                ) : (
                  <button className="btn btn-primary" disabled>Open</button>
                )}
              </div>
            </div>
          </div>
        ))}
      </div>
    </div>
  );
}

export default ObservabilityView;
