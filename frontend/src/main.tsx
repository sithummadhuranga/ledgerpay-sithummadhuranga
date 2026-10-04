import '@fontsource/ibm-plex-sans/400.css'
import '@fontsource/ibm-plex-sans/500.css'
import '@fontsource/ibm-plex-sans/600.css'
import '@fontsource/ibm-plex-mono/400.css'
import '@fontsource/ibm-plex-mono/500.css'
import '@fontsource/source-serif-4/400.css'
import '@fontsource/source-serif-4/600.css'
import '@fontsource/source-serif-4/700.css'
import './index.css'
import { StrictMode } from 'react'
import { createRoot } from 'react-dom/client'
import { createBrowserRouter, RouterProvider } from 'react-router-dom'
import { z } from 'zod'
import { Providers } from '@/app/providers'
import { routes } from '@/app/routes'
import { makeQueryClient } from '@/lib/queryClient'

// Zod compiles schemas with new Function by default, which our CSP forbids.
z.config({ jitless: true })

const router = createBrowserRouter(routes)
const client = makeQueryClient()

createRoot(document.getElementById('root')!).render(
  <StrictMode>
    <Providers client={client}>
      <RouterProvider router={router} />
    </Providers>
  </StrictMode>,
)
