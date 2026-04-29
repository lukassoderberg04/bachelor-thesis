import numpy as np
import matplotlib.pyplot as plt
from mpl_toolkits.mplot3d import Axes3D
from matplotlib.collections import PolyCollection

def get_poly(z, val):
    zs = np.concatenate([[z[0]], z, [z[-1]]])
    vals = np.concatenate([[0], val, [0]])
    return [list(zip(zs, vals))]

# Data
z = np.linspace(0, 3 * np.pi, 500)
k = 1.5
E_field = np.sin(k * z) # Vertikal komponent
B_field = np.sin(k * z) # Horisontell komponent (i fas för linjär polarisation)

fig = plt.figure(figsize=(12, 8))
ax = fig.add_subplot(111, projection='3d')

# --- Vertikalt Elektriskt fält (E) - Röd ---
ax.plot(z, E_field, zs=0, zdir='y', color='red', linewidth=2)
ax.add_collection3d(PolyCollection(get_poly(z, E_field), facecolors='red', alpha=0.1), zs=0, zdir='y')
# Etikett placerad vid den första toppen (ca z=1.0)
ax.text(1.0, 0, 1.2, r"Elektriskt fält ($\vec{E}$)", color='red', fontsize=12, fontweight='bold', ha='center')

# --- Horisontellt Magnetiskt fält (B) - Blå ---
ax.plot(z, B_field, zs=0, zdir='z', color='blue', linewidth=2)
ax.add_collection3d(PolyCollection(get_poly(z, B_field), facecolors='blue', alpha=0.1), zs=0, zdir='z')
# Etikett placerad vid den andra toppen (ca z=5.2) för att undvika krock
ax.text(5.2, 1.2, 0.3, r"Magnetiskt fält ($\vec{B}$)", color='blue', fontsize=12, fontweight='bold', ha='center')

# --- Utbredningsaxel ---
ax.quiver(0, 0, 0, z.max() + 1, 0, 0, color='black', arrow_length_ratio=0.05)
ax.text(z.max() + 0.5, 0, 0.3, "Utbredningsriktning (z)", fontsize=10, style='italic')

# Layout-inställningar
ax.set_box_aspect((4, 1, 1))
ax.view_init(elev=25, azim=-45)
ax.set_axis_off()

plt.tight_layout()
plt.show()