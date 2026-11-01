  lVar1 = *(longlong *)(param_2 + 0xb0);
  fVar13 = (float)((uint)fVar17 | 4);
  if (*(longlong *)(param_2 + 0x68) == 0) {
    fVar13 = fVar17;
  }
  fVar17 = (float)((uint)fVar13 | 8);
  if (*(longlong *)(param_2 + 0xa0) == 0) {
    fVar17 = fVar13;
  }
  fVar13 = (float)((uint)fVar17 | 0x10);
  if (*(longlong *)(param_2 + 0x58) == 0) {
    fVar13 = fVar17;
  }
  fVar17 = fVar13;
  if (lVar1 != 0) {
    fVar17 = (float)((uint)fVar13 | 0x20);
    if ((*(uint *)(lVar1 + 0x2c) & 2) == 0) {
      fVar17 = fVar13;
    }
    if ((*(uint *)(lVar1 + 0x2c) >> 0x12 & 1) != 0) {
      fVar17 = (float)((uint)fVar17 | 0x40);
    }
  }
  if (*(longlong *)(param_2 + 0xc0) != 0) {
    uVar5 = *(uint *)(*(longlong *)(param_2 + 0xc0) + 0x2c);
    fVar13 = (float)((uint)fVar17 | 0x80);
    if ((uVar5 & 0x40) == 0) {
      fVar13 = fVar17;
    }
    fVar17 = fVar13;
    if ((uVar5 >> 9 & 1) != 0) {
      fVar17 = (float)((uint)fVar17 | 0x100);
    }
  }
  fVar13 = (float)((uint)fVar17 | 0x200);
  if (*(char *)(param_2 + 0x103) == '\0') {
    fVar13 = fVar17;
  }
  if (*(float *)(param_2 + 0x7c) != 0.0) {
    fVar13 = (float)((uint)fVar13 | 0x400);
  }
  lVar8 = *(longlong *)(param_2 + 0x80);
  if (lVar8 != 0) {
    puVar12 = (uint *)&uStack_a8;
    uStack_a8 = 0x100080000;
    fVar17 = fVar15;
    do {
      fVar17 = (float)((uint)fVar17 | *puVar12);
      puVar12 = puVar12 + 1;
    } while (puVar12 != (uint *)&uStack_a0);
    fVar19 = (float)((uint)fVar13 | 0x800);
    if ((*(uint *)(lVar8 + 0x2c) & (uint)fVar17) == 0) {
      fVar19 = fVar13;
    }
    fVar13 = fVar19;
    if ((*(uint *)(lVar8 + 0x2c) & 0x100000) != 0) {
      fVar13 = (float)((uint)fVar13 | 0x1000);
    }
  }
  if (lVar1 != 0) {
    uVar5 = *(uint *)(lVar1 + 0x2c);
    fVar17 = (float)((uint)fVar13 | 0x2000);
    if ((uVar5 >> 0x15 & 1) == 0) {
      fVar17 = fVar13;
    }
    fVar13 = (float)((uint)fVar17 | 0x4000);
    if ((uVar5 >> 0x19 & 1) == 0) {
      fVar13 = fVar17;
    }
    if ((uVar5 >> 0x1a & 1) != 0) {
      fVar13 = (float)((uint)fVar13 | 0x8000);
    }
  }
  fVar17 = fVar13;
  if ((*(char *)(param_2 + 0x104) != -1) &&
     (fVar17 = (float)((uint)fVar13 | 0x20000), *(char *)(param_2 + 0x104) == '\0')) {
    fVar17 = (float)((uint)fVar13 | 0x60000);
  }
  param_1[0x3c] = fVar17;
