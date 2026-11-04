#!/usr/bin/env python3
# Life From Zero — City/Tools (ТЗ #031)
#
# Проверка раскладки карты «District750» вне Unity и вне .NET.
# Скрипт повторяет ДАННЫЕ раскладки из City/Quarter/DistrictMap/DistrictMapGenerator.cs
# (те же координаты и те же вызовы) и проверяет их:
#   * следы объектов внутри квадрата 750×750;
#   * здания и площадки не пересекают дороги, проезды и тротуары;
#   * здания не пересекаются между собой и с площадками;
#   * число жилых домов по группам (север 4, юг 3, восток 3), частный сектор 4–6;
#   * входы и точки доставки достижимы пешком от внешнего кольца (BFS, сетка 1 м);
#   * дорожный граф связен (логика RoadGraph.IsConnected).
#
# Запуск:  python3 City/Tools/district_layout_check.py
# Если меняете координаты — меняйте и здесь, и в C#-таблице (они должны совпадать).

import sys
from collections import deque

SIZE = 750
ROAD_W = 7.0
SIDEWALK_W = 2.5
CORR = ROAD_W / 2 + SIDEWALK_W   # 6 м: от оси улицы до внешнего края тротуара
DRIVE_W = 6.0

# ---------- оси улиц: линия -> (от, до) вдоль неё ----------
VLINES = {20: (20, 738), 150: (20, 738), 270: (20, 738), 420: (20, 738), 505: (20, 600), 730: (20, 738)}
HLINES = {20: (20, 730), 200: (20, 730), 300: (20, 730), 470: (20, 730), 600: (20, 730), 738: (20, 730)}
ROUNDABOUT = (270.0, 300.0)

# ---------- проезды: (x0, z0, x1, z1), ширина DRIVE_W ----------
DRIVEWAYS = [
    (492, 58, 505, 58), (492, 74, 505, 74),      # АЗС-1 -> V505
    (115, 672, 150, 672), (115, 688, 150, 688),  # АЗС-2 -> V150
    (718, 522, 730, 522), (718, 538, 730, 538),  # АЗС-3 -> V730
    (115, 332, 150, 332), (115, 348, 150, 348),  # АЗС-4 -> V150
    (345, 470, 345, 492),                        # въезд на парковку ТЦ с H470
    (85, 470, 85, 492),                          # въезд на территорию склада с H470
    (470, 600, 470, 669),                        # грузовой подъезд к заводу с H600
    (640, 600, 640, 658),                        # грузовой подъезд к складу-производству с H600
]

decor = []      # (name, rect): декоративные газоны (не зоны, без физики)
solids = []     # (name, rect): непроходимые footprints
zones = []      # (name, rect): открытая земля (газон, парковка, площадка, пад АЗС)
objects = []    # dict(type,x,y,z,rot,sx,sy,sz,params)
buildings = []  # жилые дома (многоквартирные и частные)
deliveries = [] # (id, owner, (x,z), kind)
points = []     # POI: dict(id,type,name,center,rect,entrances[(x,z)],interaction[(x,z)])
entrance_pts = []  # точки, до которых должен дойти пешеход (входы/точки доставки)


def rect_c(cx, cz, sx, sz, rot=0):
    if rot in (90, 270):
        sx, sz = sz, sx
    return (cx - sx / 2.0, cz - sz / 2.0, cx + sx / 2.0, cz + sz / 2.0)


def obj(typ, x, y, z, rot, sx, sy, sz, **params):
    objects.append(dict(type=typ, x=x, y=y, z=z, rot=rot, sx=sx, sy=sy, sz=sz, params=params))


def solid(name, rect):
    solids.append((name, rect))


def zone(name, rect):
    zones.append((name, rect))


# ---------- архетипы (зеркало BuildingArchetype/BuildingVariants в C#) ----------
ARCH = {
    'panel5': dict(floors=5, depth=12.0, fh=3.0, spacing=15.0, roof='flat', style='panel'),
    'brick3': dict(floors=3, depth=11.0, fh=3.1, spacing=12.0, roof='pitched', style='brick'),
    'tower10': dict(floors=10, depth=13.0, fh=2.9, spacing=12.0, roof='flat', style='modern'),
    'private.modern': dict(floors=2, depth=8.0, fh=3.1, spacing=11.0, roof='flat', style='modern'),
    'private.classic': dict(floors=2, depth=9.0, fh=2.9, spacing=10.0, roof='pitched', style='brick'),
}
FACE = {'N': (0, 1), 'S': (0, -1), 'E': (1, 0), 'W': (-1, 0)}
FACE_ROT = {'N': 0, 'S': 180, 'E': 90, 'W': 270}


def residential(bid, arch_id, cx, cz, rot, length, face, is_home=False):
    a = ARCH[arch_id]
    depth, height = a['depth'], a['floors'] * a['fh']
    sx, sz = (length, depth) if rot == 0 else (depth, length)
    rect = rect_c(cx, cz, sx, sz)
    solid(bid, rect)
    private = arch_id.startswith('private.')
    obj('building', cx, 0, cz, rot, length, height, depth, buildingId=bid, archetype=arch_id,
        floors=a['floors'], style=a['style'], roof=a['roof'], home=is_home)
    nx, nz = FACE[face]
    n = max(1, int(round(length / a['spacing'])))
    entr = []
    for e in range(n):
        along = length * (e + 1) / (n + 1) - length / 2.0
        ax, az = (along, 0) if rot == 0 else (0, along)
        px = cx + ax + nx * (depth / 2.0 + 0.4)
        pz = cz + az + nz * (depth / 2.0 + 0.4)
        entr.append((px, pz))
        obj('entrance', px, 0, pz, rot, 3, 3, 1, buildingId=bid, entranceNumber=e + 1)
        dx, dz = px + nx * 1.0, pz + nz * 1.0
        did = 'drop-%s-%d' % (bid, e + 1)
        deliveries.append((did, bid, (dx, dz), 'private' if private else 'residential'))
        obj('delivery.dropoff', dx, 0, dz, 0, 0.7, 0.05, 0.7, pointId=did, buildingId=bid)
        entrance_pts.append((bid, (dx, dz)))
    buildings.append(dict(id=bid, arch=arch_id, floors=a['floors'], rect=rect, center=(cx, cz),
                          rot=rot, length=length, depth=depth, height=height, home=is_home,
                          private=private, entrances=entr))


def commerce(cid, ctype, name, cx, cz, length, depth, height, face, color, n_entr=1):
    """Коммерческое здание: вход по фасаду face, точка доставки — на 1.2 м наружу."""
    rect = rect_c(cx, cz, length, depth)
    solid(cid, rect)
    obj('district.commerce', cx, 0, cz, 0, length, height, depth,
        id=cid, type=ctype, name=name, facadeColor=color)
    nx, nz = FACE[face]
    half = length / 2.0 if nx != 0 else depth / 2.0
    rot = FACE_ROT[face]
    eps = []
    for k in range(n_entr):
        if n_entr == 1:
            ex, ez = cx + nx * (half + 0.3), cz + nz * (half + 0.3)
        else:  # два входа по фасаду (ТЦ): на четверти длины фасада
            off = (-length / 4.0) if k == 0 else (length / 4.0)
            ex, ez = cx + off, cz + nz * (depth / 2.0 + 0.3)
        ix, iz = ex + nx * 1.2, ez + nz * 1.2
        eps.append((ex, ez))
        obj('district.door', ex, 0, ez, rot, 1.4, 2.3, 0.15)
        did = 'drop-%s-%d' % (cid, k + 1)
        deliveries.append((did, cid, (ix, iz), ctype))
        obj('delivery.dropoff', ix, 0, iz, 0, 0.7, 0.05, 0.7, pointId=did, buildingId=cid)
        entrance_pts.append((cid, (ix, iz)))
    sw = min(length * 0.6, 8.0) if nx == 0 else min(depth * 0.6, 8.0)
    obj('district.sign', cx + nx * (half + 0.15), 2.2, cz + nz * (half + 0.15), rot, sw, 0.8, 0.18, text=name)
    interaction = (eps[0][0] + nx * 1.2, eps[0][1] + nz * 1.2)
    points.append(dict(id=cid, type=ctype, name=name, center=(cx, cz), rect=rect,
                       entrances=eps, interaction=interaction))


def gas_station(gid, cx, cz, street_x):
    pad = rect_c(cx, cz, 60, 34)
    zone(gid + '.pad', pad)
    obj('parking.lot', cx, 0, cz, 0, 60, 0.07, 34)
    obj('gas.canopy', cx, 4.5, cz, 0, 26, 0.5, 14)
    for dx in (-12, 12):
        for dz in (-6, 6):
            obj('gas.post', cx + dx, 0, cz + dz, 0, 0.3, 4.5, 0.3)
    for ix in (cx - 6, cx + 6):
        obj('gas.island', ix, 0, cz, 0, 1.6, 0.2, 9)
        solid(gid + '.island', rect_c(ix, cz, 1.6, 9))
        for pz in (cz - 3.2, cz + 3.2):
            obj('gas.pump', ix, 0, pz, 0, 1.0, 1.4, 0.6)
            solid(gid + '.pump', rect_c(ix, pz, 1.0, 0.6))
    obj('gas.kiosk', cx - 22, 0, cz - 10, 0, 6, 3, 4)
    solid(gid + '.kiosk', rect_c(cx - 22, cz - 10, 6, 4))
    points.append(dict(id=gid, type='gas.station', name='АЗС', center=(cx, cz), rect=pad,
                       entrances=[(cx + 30, cz - 8), (cx + 30, cz + 8)], interaction=None))


def lot(lid, rect, stalls):
    zone(lid, rect)
    x0, z0, x1, z1 = rect
    w = x1 - x0
    obj('parking.lot', (x0 + x1) / 2, 0, (z0 + z1) / 2, 0, w, 0.07, z1 - z0)
    n = min(stalls, int(w // 3))
    for i in range(n):
        obj('parking.stall', x0 + (i + 0.5) * (w / n), 0, (z0 + z1) / 2, 0, 2.4, 0.06, 4.8)


def lawn(lid, rect):
    decor.append((lid, rect))
    x0, z0, x1, z1 = rect
    obj('lawn', (x0 + x1) / 2, 0, (z0 + z1) / 2, 0, x1 - x0, 0.06, z1 - z0)


def tree(x, z, crown=3.0):
    obj('tree', x, 0, z, 0, crown, 4.5, crown, crownDiameter=crown)


def bench(x, z, rot=0):
    obj('bench', x, 0, z, rot, 2.0, 0.6, 0.6)


def playground(cx, cz, sx, sz):
    zone('playground', rect_c(cx, cz, sx, sz))
    obj('playground', cx, 0, cz, 0, sx, 1.2, sz)


# ======================= СЦЕНА =======================

def build_scene():
    # --- пересечения осей и остановки на улицах ---
    crossings = set()
    stops = {}
    for x, (za, zb) in VLINES.items():
        stops[('V', x)] = {za, zb}
    for z, (xa, xb) in HLINES.items():
        stops[('H', z)] = {xa, xb}
    for x, (za, zb) in VLINES.items():
        for z, (xa, xb) in HLINES.items():
            if za <= z <= zb and xa <= x <= xb:
                crossings.add((x, z))
                stops[('V', x)].add(z)
                stops[('H', z)].add(x)
    # примыкания проездов к улицам — тоже остановки
    for (x0, z0, x1, z1) in DRIVEWAYS:
        for (px, pz) in ((x0, z0), (x1, z1)):
            if px in VLINES and VLINES[px][0] <= pz <= VLINES[px][1]:
                stops[('V', px)].add(pz)
            if pz in HLINES and HLINES[pz][0] <= px <= HLINES[pz][1]:
                stops[('H', pz)].add(px)
    # --- узлы графа ---
    nodes = {}

    def node_at(x, z):
        key = (round(x, 2), round(z, 2))
        if key not in nodes:
            nodes[key] = dict(id='node-%03d' % (len(nodes) + 1), x=x, z=z,
                              inter=(key[0], key[1]) in {(round(a, 2), round(b, 2)) for a, b in crossings})
        return nodes[key]['id']

    roads = []  # dict(a,b,kind,width,start,end)
    for x, (za, zb) in VLINES.items():
        ps = sorted(stops[('V', x)])
        for p, q in zip(ps, ps[1:]):
            node_at(x, p); node_at(x, q)
            roads.append(dict(kind='street', start=(x, p), end=(x, q), width=ROAD_W))
    for z, (xa, xb) in HLINES.items():
        ps = sorted(stops[('H', z)])
        for p, q in zip(ps, ps[1:]):
            node_at(p, z); node_at(q, z)
            roads.append(dict(kind='street', start=(p, z), end=(q, z), width=ROAD_W))
    for (x0, z0, x1, z1) in DRIVEWAYS:
        node_at(x0, z0); node_at(x1, z1)
        roads.append(dict(kind='driveway', start=(x0, z0), end=(x1, z1), width=DRIVE_W))
    return nodes, roads


def road_rects(roads):
    out = []
    for r in roads:
        (x0, z0), (x1, z1) = r['start'], r['end']
        hw = r['width'] / 2.0
        if x0 == x1:
            out.append((x0 - hw, min(z0, z1) - (0 if r['kind'] == 'driveway' else 0), x0 + hw, max(z0, z1)))
        else:
            out.append((min(x0, x1), z0 - hw, max(x0, x1), z0 + hw))
    return out


def street_corridors():
    out = []
    for x, (za, zb) in VLINES.items():
        out.append((x - CORR, za, x + CORR, zb))
    for z, (xa, xb) in HLINES.items():
        out.append((xa, z - CORR, xb, z + CORR))
    return out


def emit_streets(nodes, roads):
    """Проезжая часть, тротуары, перекрёстки, переходы, фонари (по сегментам)."""
    for r in roads:
        (x0, z0), (x1, z1) = r['start'], r['end']
        if r['kind'] == 'driveway':
            if x0 == x1:
                obj('driveway', x0, 0, (z0 + z1) / 2, 90, abs(z1 - z0), 0.1, DRIVE_W)
            else:
                obj('driveway', (x0 + x1) / 2, 0, z0, 0, abs(x1 - x0), 0.1, DRIVE_W)
            continue
        if x0 == x1:
            length = abs(z1 - z0)
            obj('road', x0, 0, (z0 + z1) / 2, 90, length, 0.1, ROAD_W)
            for side in (-1, 1):
                obj('sidewalk', x0 + side * (ROAD_W / 2 + SIDEWALK_W / 2), 0, (z0 + z1) / 2, 90,
                    length, 0.08, SIDEWALK_W)
        else:
            length = abs(x1 - x0)
            obj('road', (x0 + x1) / 2, 0, z0, 0, length, 0.1, ROAD_W)
            for side in (-1, 1):
                obj('sidewalk', (x0 + x1) / 2, 0, z0 + side * (ROAD_W / 2 + SIDEWALK_W / 2), 0,
                    length, 0.08, SIDEWALK_W)
    for key, n in nodes.items():
        if n['inter']:
            obj('intersection', n['x'], 0, n['z'], 0, ROAD_W, 0.11, ROAD_W)
            if (n['x'], n['z']) != ROUNDABOUT:
                off = ROAD_W / 2 + 2.2
                for dx, dz, rot in ((0, off, 0), (0, -off, 0), (off, 0, 90), (-off, 0, 90)):
                    obj('marking.crosswalk', n['x'] + dx, 0, n['z'] + dz, rot, ROAD_W, 0.12, 3)
    # фонари: каждые 40 м по обе стороны, чередуя
    for r in roads:
        if r['kind'] != 'street':
            continue
        (x0, z0), (x1, z1) = r['start'], r['end']
        vertical = x0 == x1
        a, b = (z0, z1) if vertical else (x0, x1)
        s = a + 20.0
        side = -1
        while s < b - 10.0:
            if vertical:
                obj('lamp.street', x0 + side * (ROAD_W / 2 + SIDEWALK_W / 2), 0, s, 0, 0.3, 6.0, 0.3)
            else:
                obj('lamp.street', s, 0, z0 + side * (ROAD_W / 2 + SIDEWALK_W / 2), 0, 0.3, 6.0, 0.3)
            side = -side
            s += 40.0
    # Ровное кольцо-круговой перекрёсток на пересечении V270 × H300.
    rx, rz = ROUNDABOUT
    obj('district.roundabout', rx, 0, rz, 0, 16.0, 0.12, 16.0)
    obj('lawn', rx, 0, rz, 0, 7.0, 0.2, 7.0)  # остров кольца: декор внутри дорожного круга


def build_district():
    nodes, roads = build_scene()
    emit_streets(nodes, roads)
    # ---------- Северный квартал (4 дома, двор с площадкой) ----------
    lawn('yard-north-w', (160, 640, 260, 680))
    playground(210, 660, 14, 10)
    bench(180, 650); bench(240, 670)
    for (tx, tz) in ((170, 646), (250, 646), (172, 674), (248, 676)):
        tree(tx, tz, 3.2)
    residential('bld-n1', 'panel5', 210, 690, 0, 30, 'S')
    residential('bld-n3', 'panel5', 210, 630, 0, 30, 'N')
    residential('bld-n2', 'brick3', 345, 690, 0, 24, 'S')
    residential('bld-n4', 'panel5', 345, 630, 0, 36, 'N', is_home=True)
    lot('park-north-e', (286, 644, 340, 662), 6)
    lot('park-north-e2', (352, 644, 404, 662), 6)
    lawn('yard-north-e', (280, 664, 410, 680))
    tree(296, 674, 3.0); tree(392, 674, 3.0)
    # ---------- Южный квартал (3 дома, двор, парковка) ----------
    lawn('yard-south', (160, 70, 262, 140))
    playground(200, 100, 40, 16)
    bench(230, 120)
    tree(176, 124, 3.2); tree(246, 122, 3.2); tree(178, 80, 3.2)
    lot('park-south', (228, 72, 258, 100), 6)
    residential('bld-s1', 'panel5', 210, 150, 0, 30, 'S')
    residential('bld-s2', 'panel5', 210, 60, 0, 30, 'N')
    residential('bld-s3', 'panel5', 340, 105, 90, 30, 'W')
    lot('park-south-w', (280, 95, 325, 115), 6)
    # ---------- Восточный квартал (3 современных дома) ----------
    lot('park-east-1', (520, 215, 715, 250), 14)
    lot('park-east-2', (530, 320, 700, 355), 12)
    lawn('yard-east', (550, 362, 680, 376))
    tree(560, 368, 3.0); tree(615, 368, 3.0); tree(670, 368, 3.0)
    residential('bld-e1', 'tower10', 540, 400, 90, 30, 'E')
    residential('bld-e2', 'tower10', 620, 400, 90, 30, 'W')
    residential('bld-e3', 'tower10', 690, 400, 90, 30, 'W')
    # ---------- Частный сектор (5 домов, юго-восток) ----------
    residential('bld-p1', 'private.modern', 545, 150, 0, 11, 'N')
    residential('bld-p2', 'private.classic', 605, 150, 0, 10, 'N')
    residential('bld-p3', 'private.modern', 665, 150, 0, 11, 'N')
    residential('bld-p4', 'private.classic', 575, 80, 0, 10, 'S')
    residential('bld-p5', 'private.modern', 640, 80, 0, 11, 'S')
    # ---------- Центр ----------
    commerce('com-mall', 'mall', 'ТЦ «ГОРИЗОНТ»', 345, 535, 120, 50, 9.0, 'S', '#E8E4DC', n_entr=2)
    lot('mall-park-w', (285, 480, 339, 504), 10)
    lot('mall-park-e', (351, 480, 405, 504), 10)
    commerce('com-bank', 'bank', 'БАНК', 390, 380, 30, 22, 7.0, 'E', '#2F5F8A')
    commerce('com-city-hall', 'city.hall', 'МЭРИЯ', 330, 430, 44, 24, 7.5, 'N', '#D9CFA8')
    lawn('center-square', (300, 350, 362, 400))
    tree(312, 360, 3.0); tree(350, 395, 3.0); tree(316, 392, 3.0)
    bench(338, 362); bench(322, 404)
    # ---------- Бизнес-зона (центр-восток) ----------
    commerce('com-business', 'business.center', 'БИЗНЕС-ЦЕНТР', 470, 428, 36, 22, 16.0, 'E', '#5B6F82')
    commerce('com-police', 'police', 'ПОЛИЦИЯ', 470, 378, 30, 18, 6.0, 'E', '#34495E')
    commerce('com-hospital', 'hospital', 'БОЛЬНИЦА', 470, 333, 36, 22, 8.0, 'E', '#E9EDF0')
    lot('biz-park', (430, 215, 495, 290), 8)
    commerce('com-incubator', 'incubator', 'БИЗНЕС-ИНКУБАТОР', 345, 250, 40, 26, 9.0, 'S', '#7A8C6E')
    commerce('com-education', 'education', 'КУРСЫ', 462, 150, 40, 22, 6.0, 'E', '#C9A66B')
    # ---------- Левый блок: Продукты, кафе, одежда, строймаркет, ломбард, ПВЗ, склад ----------
    commerce('shop-01', 'grocery', 'ПРОДУКТЫ', 210, 250, 44, 26, 3.4, 'W', '#C8D8C0')
    commerce('com-cafe', 'cafe', 'КАФЕ', 190, 425, 26, 16, 4.0, 'W', '#D8A878')
    commerce('com-restaurant', 'restaurant', 'РЕСТОРАН', 190, 390, 26, 16, 4.6, 'W', '#8E3B3B')
    commerce('com-clothing', 'clothing', 'ОДЕЖДА', 190, 355, 26, 16, 4.0, 'W', '#A9B7C6')
    commerce('com-hardware', 'hardware', 'СТРОЙМАРКЕТ', 85, 250, 70, 44, 8.0, 'E', '#D1A44A')
    lot('hardware-park', (40, 276, 130, 290), 10)
    commerce('com-pawnshop', 'pawnshop', 'ЛОМБАРД', 85, 425, 30, 18, 5.0, 'E', '#4F5D75')
    commerce('com-warehouse', 'warehouse', 'СКЛАД', 85, 535, 70, 36, 8.0, 'S', '#8C8C86')
    lot('warehouse-yard-w', (52, 480, 82, 510), 0)
    lot('warehouse-yard-e', (88, 480, 118, 510), 0)
    # ПВЗ (зеркало PickupPointInfo): фасад на юг
    pvz_center = (85.0, 390.0)
    commerce_pvz_front = 386.0
    solid('pvz-01', rect_c(85, 390, 12, 8))
    obj('pickup.building', 85, 0, 390, 0, 12, 3.2, 8,
        buildingId='pvz-01', signText='ПУНКТ ВЫДАЧИ ЗАКАЗОВ')
    obj('pickup.plaza', 85, 0, 384, 0, 14, 0.07, 4)
    zone('pvz-01.plaza', rect_c(85, 384, 14, 4))
    obj('pickup.point', 85, 0, 384.4, 0, 0.8, 0.05, 0.8, purpose='delivery.pickup')
    points.append(dict(id='pvz-01', type='pickup', name='ПУНКТ ВЫДАЧИ ЗАКАЗОВ', center=pvz_center,
                       rect=rect_c(85, 390, 12, 8), entrances=[(85, 385.7)], interaction=(85, 384.4)))
    deliveries.append(('drop-pvz-01', 'pvz-01', (85, 384.4), 'pickup'))
    entrance_pts.append(('pvz-01', (85, 384.4)))
    # ---------- Западный блок: АЗС-4 под ломбардом ----------
    gas_station('gas-04', 85, 340, 150)
    gas_station('gas-02', 85, 680, 150)
    gas_station('gas-01', 462, 66, 505)
    gas_station('gas-03', 688, 530, 730)
    # ---------- Автомобильный сервис ----------
    commerce('com-carservice', 'car.service', 'АВТОСЕРВИС', 535, 535, 36, 24, 6.0, 'S', '#6E7B8B')
    commerce('com-tuning', 'tuning', 'ТЮНИНГ', 600, 535, 36, 24, 6.0, 'S', '#2B2D42')
    lot('auto-park', (511, 480, 724, 505), 16)
    # ---------- Промзона (северо-восток) ----------
    solid('factory', rect_c(500, 690, 100, 40))
    obj('industry.building', 500, 0, 690, 0, 100, 12.0, 40, facadeColor='#6B6F73', name='ЗАВОД')
    obj('industry.chimney', 532, 0, 712, 0, 2.4, 30.0, 2.4)
    obj('industry.chimney', 538, 0, 712, 0, 2.4, 30.0, 2.4)
    points.append(dict(id='ind-factory', type='factory', name='ЗАВОД', center=(500, 690),
                       rect=rect_c(500, 690, 100, 40), entrances=[(470, 669)], interaction=(478, 666)))
    deliveries.append(('drop-ind-factory-gate', 'ind-factory', (478, 666), 'logistics'))
    entrance_pts.append(('ind-factory', (478, 666)))
    solid('warehouse-prod', rect_c(640, 690, 80, 30))
    obj('industry.building', 640, 0, 690, 0, 80, 8.0, 30, facadeColor='#7D8B6A', name='ПРОИЗВОДСТВО')
    obj('industry.loading', 640, 0, 662, 0, 70, 0.9, 8)
    points.append(dict(id='ind-warehouse', type='industry.warehouse', name='ПРОИЗВОДСТВО/СКЛАД',
                       center=(640, 690), rect=rect_c(640, 690, 80, 30), entrances=[(640, 674)],
                       interaction=(652, 662)))
    deliveries.append(('drop-ind-loading-dock', 'ind-warehouse', (652, 662), 'logistics'))
    entrance_pts.append(('ind-warehouse', (652, 662)))
    # ---------- Лесопарк ----------
    lawn('forest-park', (26, 26, 144, 194))
    for (tx, tz) in [(x, z) for x in range(40, 140, 12) for z in range(40, 194, 14)][:40]:
        if (tx + tz) % 3 == 0:
            continue
        tree(tx + ((tz * 7) % 5) - 2, tz, 3.4)
    bench(60, 120); bench(100, 60)
    obj('yard.footpath', 85, 0, 110, 0, 118, 0.07, 2.0)
    # ---------- Сквер на центре-юго-западе ----------
    lawn('square-cw', (156, 476, 264, 594))
    tree(180, 500, 3.0); tree(240, 500, 3.0); tree(210, 560, 3.0)
    bench(200, 520)
    return nodes, roads


# ======================= ПРОВЕРКИ =======================

def overlap(a, b, eps=1e-6):
    return a[0] < b[2] - eps and b[0] < a[2] - eps and a[1] < b[3] - eps and b[1] < a[3] - eps


def check_all():
    errors = []
    nodes, roads = build_district()

    # 1. Границы
    for name, r in solids + zones + decor:
        if r[0] < 0 or r[1] < 0 or r[2] > SIZE or r[3] > SIZE:
            errors.append('outside bounds: ' + name + str(r))

    # 2. Здания/площадки не пересекают улицы и проезды
    corridors = street_corridors()
    drv = road_rects(roads)
    for name, r in solids + zones + decor:
        for c in corridors:
            if overlap(r, c):
                errors.append('solid/zone %s overlaps street corridor %s' % (name, c))
        for d in drv:
            if overlap(r, d):
                errors.append('solid/zone %s overlaps driveway %s' % (name, d))

    # 3. Непересечение solid/zone между собой (буфер 0)
    items = solids + zones
    for i in range(len(items)):
        for j in range(i + 1, len(items)):
            a_name, b_name = items[i][0], items[j][0]
            if a_name != b_name and a_name.split('.')[0] == b_name.split('.')[0]:
                continue  # части одного объекта (пад АЗС с колонками/киоском); одинаковые имена — это дубль
            if overlap(items[i][1], items[j][1]):
                errors.append('overlap: %s vs %s' % (a_name, b_name))
    for dn, dr in decor:  # декор-газон может содержать площадки/парковки дворов, но не здания
        for name, r in solids:
            if overlap(dr, r):
                errors.append('decor %s overlaps building %s' % (dn, name))

    # 3a. Дубли: одинаковые имена, одинаковые объекты (тип и координаты), одинаковые дороги
    seen_names = {}
    for name, r in solids + zones + decor:
        key = (name, tuple(round(v, 2) for v in r))  # одинаковое имя И одинаковый прямоугольник = дубль
        seen_names[key] = seen_names.get(key, 0) + 1
    for key, n in seen_names.items():
        if n > 1:
            errors.append('duplicate solid/zone: %s %s (x%d)' % (key[0], key[1], n))
    seen_obj = {}
    for o in objects:
        key = (o['type'], round(o['x'], 2), round(o['y'], 2), round(o['z'], 2), round(o.get('rot', 0), 1))
        seen_obj[key] = seen_obj.get(key, 0) + 1
    dup_objs = [k for k, n in seen_obj.items() if n > 1]
    for k in dup_objs[:20]:
        errors.append('duplicate object: %s at (%.2f, %.2f, %.2f)' % (k[0], k[1], k[2], k[3]))
    seen_road = {}
    for r in roads:
        key = (tuple(r['start']), tuple(r['end']))
        key = min(key, (key[1], key[0]))
        seen_road[key] = seen_road.get(key, 0) + 1
    for k, n in seen_road.items():
        if n > 1:
            errors.append('duplicate road segment: %s - %s' % (k[0], k[1]))

    # 3b. Запрещённые объекты ТЗ №031 (мойка, фитнес, вода, железная дорога) — по именам и типам
    FORBIDDEN = ('wash', 'мойк', 'gym', 'fitness', 'фитнес', 'river', 'lake', 'water', 'рек', 'озер', 'вод',
                 'rail', 'railway', 'железн', 'жд')
    all_names = [name for name, _r in solids + zones + decor] + [o['type'] for o in objects] + \
                [b.get('id', '') for b in buildings] + [p['id'] for p in points]
    for nm in all_names:
        low = nm.lower()
        for w in FORBIDDEN:
            if w in low:
                errors.append('forbidden object name/type: %s (match %r)' % (nm, w))
                break

    # 4. Счётчики
    mkd = [b for b in buildings if not b['private']]
    priv = [b for b in buildings if b['private']]
    north = [b for b in mkd if b['center'][1] > 600]
    south = [b for b in mkd if b['center'][1] < 200]
    east = [b for b in mkd if b['center'][0] > 500 and 300 <= b['center'][1] <= 470]
    if len(mkd) != 10: errors.append('MKD count %d != 10' % len(mkd))
    if len(north) != 4: errors.append('north %d != 4' % len(north))
    if len(south) != 3: errors.append('south %d != 3' % len(south))
    if len(east) != 3: errors.append('east %d != 3' % len(east))
    if not (4 <= len(priv) <= 6): errors.append('private %d not in 4..6' % len(priv))
    homes = [b for b in mkd if b['home']]
    if len(homes) != 1: errors.append('player home count %d' % len(homes))

    # 5. Обязательные объекты
    need = {'com-mall': 'ТЦ', 'shop-01': 'продукты', 'com-cafe': 'кафе', 'com-restaurant': 'ресторан',
            'com-clothing': 'одежда', 'com-hardware': 'строймаркет', 'com-pawnshop': 'ломбард',
            'com-business': 'бизнес-центр', 'com-incubator': 'инкубатор', 'com-bank': 'банк',
            'pvz-01': 'ПВЗ', 'com-city-hall': 'мэрия', 'com-police': 'полиция',
            'com-hospital': 'больница', 'com-education': 'обучение', 'com-carservice': 'автосервис',
            'com-tuning': 'тюнинг', 'com-warehouse': 'склад', 'ind-factory': 'завод',
            'ind-warehouse': 'производство', 'gas-01': 'АЗС-1', 'gas-02': 'АЗС-2',
            'gas-03': 'АЗС-3', 'gas-04': 'АЗС-4'}
    ids = {p['id'] for p in points}
    for k, label in need.items():
        if k not in ids:
            errors.append('missing: ' + label + ' (' + k + ')')

    # 6. Входы и точки доставки — пешком от кольца (BFS по сетке 1 м)
    blocked = [r for _, r in solids]
    N = int(SIZE)
    grid = [[True] * N for _ in range(N)]
    for r in blocked:
        for ix in range(max(0, int(r[0])), min(N, int(r[2]) + 1)):
            for iz in range(max(0, int(r[1])), min(N, int(r[3]) + 1)):
                if r[0] <= ix + 0.5 <= r[2] and r[1] <= iz + 0.5 <= r[3]:
                    grid[ix][iz] = False
    seen = [[False] * N for _ in range(N)]
    dq = deque()
    for ix in range(N):
        for iz in (0, 1, N - 2, N - 1):
            if grid[ix][iz] and not seen[ix][iz]:
                seen[ix][iz] = True; dq.append((ix, iz))
    for iz in range(N):
        for ix in (0, 1, N - 2, N - 1):
            if grid[ix][iz] and not seen[ix][iz]:
                seen[ix][iz] = True; dq.append((ix, iz))
    while dq:
        x, z = dq.popleft()
        for dx, dz in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            nx_, nz_ = x + dx, z + dz
            if 0 <= nx_ < N and 0 <= nz_ < N and grid[nx_][nz_] and not seen[nx_][nz_]:
                seen[nx_][nz_] = True; dq.append((nx_, nz_))
    unreachable = []
    for owner, (px, pz) in entrance_pts:
        ix, iz = int(px), int(pz)  # строго клетка точки: стена вокруг входа = недостижимо
        ok = 0 <= ix < N and 0 <= iz < N and seen[ix][iz]
        if not ok:
            unreachable.append((owner, px, pz))
    for u in unreachable:
        errors.append('unreachable entrance/delivery: %s at (%.1f, %.1f)' % u)

    # 7. Точки доставки (количество/типы)
    kinds = {}
    for _, _, _, k in deliveries:
        kinds[k] = kinds.get(k, 0) + 1

    # 8. Связность дорожного графа (как RoadGraph.IsConnected)
    adj = {n['id']: set() for n in nodes.values()}
    pos = {n['id']: (n['x'], n['z']) for n in nodes.values()}
    for r in roads:
        a = node_id(nodes, r['start']); b = node_id(nodes, r['end'])
        adj[a].add(b); adj[b].add(a)
    for r in roads:
        for nid, p in pos.items():
            if nid in (node_id(nodes, r['start']), node_id(nodes, r['end'])):
                continue
            if on_seg(p, r['start'], r['end']):
                a = node_id(nodes, r['start']); b = node_id(nodes, r['end'])
                adj[nid].add(a); adj[a].add(nid); adj[nid].add(b); adj[b].add(nid)
    start = next(iter(adj))
    vis = {start}; q = deque([start])
    while q:
        c = q.popleft()
        for n2 in adj[c]:
            if n2 not in vis:
                vis.add(n2); q.append(n2)
    if len(vis) != len(adj):
        errors.append('road graph disconnected: %d/%d' % (len(vis), len(adj)))

    # 9. Деревья не внутри зданий
    for o in objects:
        if o['type'] == 'tree':
            for name, r in solids:
                if r[0] - 1.5 < o['x'] < r[2] + 1.5 and r[1] - 1.5 < o['z'] < r[3] + 1.5:
                    errors.append('tree inside %s at (%.1f,%.1f)' % (name, o['x'], o['z']))
                    break

    # 10. Объекты в пределах карты
    for o in objects:
        if not (0 <= o['x'] <= SIZE and 0 <= o['z'] <= SIZE):
            errors.append('object outside map: %s (%.1f,%.1f)' % (o['type'], o['x'], o['z']))

    report = dict(
        entrances_checked=len(entrance_pts), unreachable=len(unreachable),
        walkable_cells=sum(1 for x in range(N) for z in range(N) if seen[x][z]),
        objects=len(objects), solids=len(solids), zones=len(zones), decor=len(decor), roads=len(roads), nodes=len(nodes),
        buildings_mkd=len(mkd), north=len(north), south=len(south), east=len(east), private=len(priv),
        points=len(points), deliveries=len(deliveries), delivery_kinds=kinds,
        trees=sum(1 for o in objects if o['type'] == 'tree'),
        lamps=sum(1 for o in objects if o['type'] == 'lamp.street'),
        stalls=sum(1 for o in objects if o['type'] == 'parking.stall'),
    )
    return errors, report


def node_id(nodes, p):
    key = (round(p[0], 2), round(p[1], 2))
    return nodes[key]['id']


def on_seg(p, a, b, eps=0.01):
    if abs(a[0] - b[0]) < eps:
        return abs(p[0] - a[0]) < eps and min(a[1], b[1]) - eps <= p[1] <= max(a[1], b[1]) + eps
    if abs(a[1] - b[1]) < eps:
        return abs(p[1] - a[1]) < eps and min(a[0], b[0]) - eps <= p[0] <= max(a[0], b[0]) + eps
    return False


if __name__ == '__main__':
    errs, rep = check_all()
    for k, v in rep.items():
        print('%-16s %s' % (k, v))
    if errs:
        print('\nОШИБКИ (%d):' % len(errs))
        for e in errs[:80]:
            print('  -', e)
        sys.exit(1)
    print('\nOK: все проверки пройдены')
