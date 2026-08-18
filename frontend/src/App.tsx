import { createBrowserRouter, RouterProvider } from 'react-router'
import HomePage from './pages/HomePage'

const router = createBrowserRouter([
  {
    path: '/',
    element: <HomePage />,
  },
])

function App() {
  return <RouterProvider router={router} />
}

export default App