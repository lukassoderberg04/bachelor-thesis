import numpy as np
import matplotlib.pyplot as plt

# 1. Skapa data för ljusets utbredning längs z-axeln
z = np.linspace(0, 4 * np.pi, 200)

# Cirkulär polarisation: Kombinerar sinus och cosinus (fasskiftet)
Ex_circ = np.cos(z)
Ey_circ = np.sin(z)

# 2. Setup för figuren
fig = plt.figure(figsize=(10, 8))
ax = fig.add_subplot(111, projection='3d')

# Rita ut den cirkulärt polariserade vågen
ax.plot(z, Ex_circ, Ey_circ, color='purple', lw=2.5)

# Rita ut fältvektorer (pilar) för att visa rotationen
for i in range(0, len(z), 10):
    ax.quiver(z[i], 0, 0, 0, Ex_circ[i], Ey_circ[i], color='purple', alpha=0.3, arrow_length_ratio=0.1)

# Rita ut mittlinje för färdriktningen
ax.plot(z, np.zeros_like(z), np.zeros_like(z), color='black', lw=1.5)

# Pil för färdriktningen i slutet av z-axeln
ax.quiver(z[-1], 0, 0, 1.5, 0, 0, color='black', lw=2, arrow_length_ratio=0.3)

# 🎯 LÄGG TILL ETIKETT FÖR DET ELEKTRISKA FÄLTET
# Vi placerar ett "E" med en vektorpil ovanpå ungefär mitt på spiralen
idx_label = 100  # Index mitt i dataserien (vid z = 2 * pi)
ax.text(z[idx_label], Ex_circ[idx_label] + 0.2, Ey_circ[idx_label], 
        r'$\vec{E}$', color='purple', fontsize=18, fontweight='bold')

# 3. Rita ut ett statiskt projektionsplan vid slutet (z = 4 * pi)
theta = np.linspace(0, 2*np.pi, 100)
x_circle = np.cos(theta)
y_circle = np.sin(theta)
z_circle = np.ones_like(theta) * z[-1]

# Rita den streckade cirkeln på projektionsplanet
ax.plot(z_circle, x_circle, y_circle, color='gray', linestyle='--', lw=1.5, alpha=0.7)

# Rita ut en lätt tonad fylld yta för planet för att ge djupkänsla
xx, yy = np.meshgrid(np.linspace(-1.2, 1.2, 10), np.linspace(-1.2, 1.2, 10))
zz = np.ones_like(xx) * z[-1]
ax.plot_surface(zz, xx, yy, color='gray', alpha=0.1)

# Ta bort alla axlar, skalstreck och bakgrundsrutnät
ax.set_axis_off()

# Justera kameravinkeln för ett tydligt djupseende
ax.view_init(elev=20, azim=-45)

plt.tight_layout()
plt.show()