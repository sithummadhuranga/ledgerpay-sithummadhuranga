import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import './index.css'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { Providers } from '@/app/providers'
import { routes } from '@/app/routes'
import { makeQueryClient } from '@/lib/queryClient'

const router = createBrowserRouter(routes)
const client = makeQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Providers client={client}>
      <RouterProvider router={router} />
    </Providers>
  </StrictMode>,
)
