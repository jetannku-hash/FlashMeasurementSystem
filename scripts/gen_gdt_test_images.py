"""
GD&T v1 合成測試影像產生器（供 GUI 目視，非 ground truth）。

誠實限制：
- 這些是「畫圖時設定已知缺陷量」的合成灰階圖，用來在 GUI 跑整條管線、目視 overlay
  與 OK/NG 是否合理。
- HALCON 次像素邊緣抽取會自帶誤差，跑出來的數值不會精確等於此處設定的幅值，只能
  粗驗「方向對、量級對」。
- 這層驗的是「管線接線」，不是「量真實零件準度」（後者需實體標準件＋相機＋對標 CMM）。

輸出：../data/images/gdt_*.png（灰階；淺底深特徵，供邊緣偵測）。
用法：python scripts/gen_gdt_test_images.py
"""
import os
import math
import numpy as np
from PIL import Image

W, H = 800, 600
BG, FG = 220, 40   # 淺底、深特徵

OUT_DIR = os.path.join(os.path.dirname(__file__), "..", "data", "images")


def new_canvas():
    return np.full((H, W), BG, dtype=np.uint8)


def save(arr, name):
    os.makedirs(OUT_DIR, exist_ok=True)
    path = os.path.join(OUT_DIR, name)
    Image.fromarray(arr, mode="L").save(path)
    print("wrote", os.path.normpath(path))


def yx_grid():
    yy, xx = np.mgrid[0:H, 0:W]
    return yy.astype(float), xx.astype(float)


def roundness_lobed(cr=300, cc=400, R=140.0, amp=4.0, lobes=3):
    # 半徑 R + amp*cos(lobes*theta) 的填實瓣形盤。峰對峰徑向偏差 = 2*amp。
    yy, xx = yx_grid()
    theta = np.arctan2(yy - cr, xx - cc)
    rad = np.hypot(yy - cr, xx - cc)
    edge = R + amp * np.cos(lobes * theta)
    arr = new_canvas()
    arr[rad <= edge] = FG
    save(arr, "gdt_roundness_lobed.png")


def straightness_bow(top=220, bottom=380, amp=5.0):
    # 深長條，其「上緣」為弓形 row = top + amp*sin(pi * col_frac)。line 工具量上緣真直度。
    yy, xx = yx_grid()
    col_frac = (xx - 120) / 560.0
    bow = top + amp * np.sin(np.pi * np.clip(col_frac, 0, 1))
    arr = new_canvas()
    inside = (xx >= 120) & (xx <= 680) & (yy >= bow) & (yy <= bottom)
    arr[inside] = FG
    save(arr, "gdt_straightness_bow.png")


def _tilted_bar(arr, cr, cc, length, half_thick, angle_deg):
    # 在 (cr,cc) 放一條長 length、半厚 half_thick、傾角 angle_deg 的深長條。
    yy, xx = yx_grid()
    a = math.radians(angle_deg)
    # 旋轉到長條本地座標
    u = (xx - cc) * math.cos(a) + (yy - cr) * math.sin(a)   # 沿長軸
    v = -(xx - cc) * math.sin(a) + (yy - cr) * math.cos(a)  # 垂直長軸
    inside = (np.abs(u) <= length / 2.0) & (np.abs(v) <= half_thick)
    arr[inside] = FG


def parallelism(angle_deg=5.0):
    # 兩條深長條：上條水平、下條偏 angle_deg。兩 line 工具各量一條 → 平行度。
    arr = new_canvas()
    _tilted_bar(arr, 200, 400, 520, 10, 0.0)
    _tilted_bar(arr, 400, 400, 520, 10, angle_deg)
    save(arr, "gdt_parallelism.png")


def perpendicularity(off_deg=3.0):
    # 一橫一直長條，直條偏離垂直 off_deg。兩 line 工具 → 垂直度。
    arr = new_canvas()
    _tilted_bar(arr, 300, 250, 360, 10, 0.0)            # 水平基準
    _tilted_bar(arr, 300, 250, 360, 10, 90.0 - off_deg) # 近垂直量測線
    save(arr, "gdt_perpendicularity.png")


def concentricity(offset=6.0):
    # 外圓盤 + 偏心內孔（深環帶內孔）。外圓/內孔各一 circle 工具 → 同心度（直徑帶=2*offset）。
    yy, xx = yx_grid()
    cr, cc = 300.0, 400.0
    r_outer = 160.0
    r_inner = 70.0
    rad_outer = np.hypot(yy - cr, xx - cc)
    rad_inner = np.hypot(yy - (cr + offset), xx - cc)  # 內孔中心沿 row 偏移 offset px（圓心距=offset）
    arr = new_canvas()
    arr[(rad_outer <= r_outer) & (rad_inner >= r_inner)] = FG  # 環帶（外盤挖偏心孔）
    save(arr, "gdt_concentricity.png")


def main():
    roundness_lobed()      # 峰對峰 ≈ 8 px
    straightness_bow()     # 弓幅 5 px
    parallelism(5.0)       # 兩線夾 5°
    perpendicularity(3.0)  # 偏離垂直 3°
    concentricity(6.0)     # 圓心偏移 6 px → 直徑帶 ≈ 12 px
    print("done. 提醒：合成圖供目視，非 ground truth；HALCON 次像素自帶誤差。")


if __name__ == "__main__":
    main()
