// FUN_14217ebf0 @ 14217ebf0

/* WARNING: Globals starting with '_' overlap smaller symbols at the same address */

void FUN_14217ebf0(float *param_1,longlong param_2)

{
  longlong lVar1;
  float fVar2;
  byte bVar3;
  byte bVar4;
  uint uVar5;
  uint uVar6;
  longlong *plVar7;
  longlong lVar8;
  undefined8 uVar9;
  int iVar10;
  undefined8 *puVar11;
  uint *puVar12;
  float fVar13;
  float fVar14;
  float fVar15;
  float fVar16;
  float fVar17;
  float fVar18;
  float fVar19;
  float fVar20;
  float fVar21;
  undefined8 uStack_a8;
  undefined8 uStack_a0;
  
  fVar13 = _UNK_143560150;
  uVar5 = *(uint *)(param_2 + 0x18);
  uVar6 = *(uint *)(param_2 + 0x14);
  fVar20 = (float)(uVar5 >> 0x10 & 0xff) * _UNK_1436525fc * (float)(uVar6 >> 0x10 & 0xff);
  fVar19 = (float)(uVar5 >> 8 & 0xff) * _UNK_1436525fc * (float)(uVar6 >> 8 & 0xff);
  fVar17 = (float)(uVar5 & 0xff) * _UNK_1436525fc * (float)(uVar6 & 0xff);
  fVar15 = (float)(uVar5 >> 0x18) * _UNK_1436525fc * (float)(uVar6 >> 0x18);
  *param_1 = fVar20;
  param_1[1] = fVar19;
  param_1[2] = fVar17;
  param_1[3] = fVar15;
  uVar5 = *(uint *)(param_2 + 0x1c);
  uStack_a8 = CONCAT44(fVar19,fVar20);
  uStack_a0 = CONCAT44(fVar15,fVar17);
  fVar15 = (float)func_0x000143324beb((float)(uVar5 >> 0x10 & 0xff) * fVar13);
  fVar17 = (float)func_0x000143324beb((float)(uVar5 >> 8 & 0xff) * fVar13);
  fVar20 = (float)func_0x000143324beb((float)(uVar5 & 0xff) * fVar13);
  fVar19 = _UNK_143c844b4;
  param_1[4] = fVar15;
  param_1[5] = fVar17;
  param_1[6] = fVar20;
  param_1[7] = (float)(uVar5 >> 0x18) * fVar13;
  param_1[7] = (float)*(byte *)(param_2 + 0x25) * fVar13;
  fVar15 = *(float *)(param_2 + 0x40);
  fVar17 = *(float *)(param_2 + 0x80);
  fVar18 = (float)*(byte *)(param_2 + 0x10) * fVar13;
  fVar20 = (float)*(byte *)(param_2 + 0x11) * fVar13;
  param_1[8] = fVar18;
  param_1[9] = fVar20;
  param_1[10] = fVar15;
  param_1[0xb] = fVar17;
  uStack_a8 = CONCAT44(fVar20,fVar18);
  uStack_a0 = CONCAT44(fVar17,fVar15);
  fVar17 = fVar19;
  fVar15 = fVar19;
  if (*(longlong **)(param_2 + 0x90) != (longlong *)0x0) {
    iVar10 = (**(code **)(**(longlong **)(param_2 + 0x90) + 0x18))(fVar20);
    fVar15 = (float)iVar10;
    iVar10 = (**(code **)(**(longlong **)(param_2 + 0x90) + 0x20))();
    fVar17 = (float)iVar10;
  }
  fVar20 = *(float *)(param_2 + 0x20);
  fVar14 = (float)*(byte *)(param_2 + 0x12) * fVar13;
  fVar16 = (float)*(byte *)(param_2 + 0x13) * fVar13;
  param_1[0xc] = fVar14;
  param_1[0xd] = fVar16;
  param_1[0xe] = fVar20;
  param_1[0xf] = 0.0;
  plVar7 = *(longlong **)(param_2 + 0xa0);
  fVar18 = *(float *)(param_2 + 0x128);
  uStack_a8 = CONCAT44(fVar16,fVar14);
  uStack_a0 = (ulonglong)(uint)fVar20;
  fVar21 = (float)((uint)(fVar19 / ((*(float *)(param_2 + 0x124) - fVar18) + _UNK_143c8435c)) &
                  _UNK_143c847d0);
  fVar20 = fVar19;
  fVar14 = fVar19;
  if (plVar7 != (longlong *)0x0) {
    iVar10 = (**(code **)(*plVar7 + 0x18))(plVar7,fVar16);
    fVar14 = (float)iVar10;
    iVar10 = (**(code **)(**(longlong **)(param_2 + 0xa0) + 0x20))();
    fVar20 = (float)iVar10;
  }
  fVar16 = *(float *)(param_2 + 0x34);
  fVar2 = *(float *)(param_2 + 0x3c);
  param_1[0x10] = (fVar14 / fVar20) * fVar16;
  param_1[0x11] = fVar16;
  param_1[0x12] = fVar2;
  param_1[0x13] = fVar21;
  fVar20 = *(float *)(param_2 + 0x38);
  fVar14 = *(float *)(param_2 + 0x44);
  param_1[0x14] = (fVar15 / fVar17) * fVar20;
  param_1[0x15] = fVar20;
  param_1[0x16] = fVar14;
  param_1[0x17] = fVar18;
  bVar3 = *(byte *)(param_2 + 0x30);
  fVar15 = *(float *)(param_2 + 0x84);
  bVar4 = *(byte *)(param_2 + 0x31);
  param_1[0x18] = (float)bVar3;
  param_1[0x19] = (float)bVar4;
  param_1[0x1a] = fVar15;
  param_1[0x1b] = 0.0;
  uVar5 = *(uint *)(param_2 + 0x68);
  uStack_a8 = CONCAT44((float)bVar4,(float)bVar3);
  uStack_a0 = (ulonglong)(uint)fVar15;
  fVar15 = (float)func_0x000143324beb((float)(uVar5 >> 0x10 & 0xff) * fVar13);
  fVar17 = (float)func_0x000143324beb((float)(uVar5 >> 8 & 0xff) * fVar13);
  fVar20 = (float)func_0x000143324beb((float)(uVar5 & 0xff) * fVar13);
  param_1[0x1c] = fVar15;
  param_1[0x1d] = fVar17;
  param_1[0x1e] = fVar20;
  param_1[0x1f] = (float)(uVar5 >> 0x18) * fVar13;
  param_1[0x1f] = *(float *)(param_2 + 0x6c);
  lVar1 = (longlong)*(char *)(param_2 + 0x10d) * 0x10;
  uVar9 = *(undefined8 *)(lVar1 + 0x144cb6378);
  *(undefined8 *)(param_1 + 0x20) = *(undefined8 *)(lVar1 + 0x144cb6370);
  *(undefined8 *)(param_1 + 0x22) = uVar9;
  lVar1 = (longlong)*(char *)(param_2 + 0x10e) * 0x10;
  uVar9 = *(undefined8 *)(lVar1 + 0x144cb6378);
  *(undefined8 *)(param_1 + 0x24) = *(undefined8 *)(lVar1 + 0x144cb6370);
  *(undefined8 *)(param_1 + 0x26) = uVar9;
  lVar1 = (longlong)*(char *)(param_2 + 0x10f) * 0x10;
  uVar9 = *(undefined8 *)(lVar1 + 0x144cb6378);
  *(undefined8 *)(param_1 + 0x28) = *(undefined8 *)(lVar1 + 0x144cb6370);
  *(undefined8 *)(param_1 + 0x2a) = uVar9;
  fVar15 = *(float *)(param_2 + 0x114);
  param_1[0x2c] = 0.0;
  param_1[0x2d] = fVar15;
  param_1[0x2e] = 0.0;
  param_1[0x2f] = 0.0;
  fVar15 = (float)*(byte *)(param_2 + 0x26) * fVar13;
  fVar17 = (float)*(byte *)(param_2 + 0x27) * fVar13;
  param_1[0x34] = fVar15;
  param_1[0x35] = fVar17;
  param_1[0x36] = 0.0;
  param_1[0x37] = 0.0;
  uVar5 = *(uint *)(param_2 + 0x28);
  uStack_a8 = CONCAT44(fVar17,fVar15);
  uStack_a0 = 0;
  fVar15 = (float)func_0x000143324beb((float)(uVar5 >> 0x10 & 0xff) * fVar13);
  fVar17 = (float)func_0x000143324beb((float)(uVar5 >> 8 & 0xff) * fVar13);
  fVar20 = (float)func_0x000143324beb((float)(uVar5 & 0xff) * fVar13);
  param_1[0x30] = fVar15;
  param_1[0x31] = fVar17;
  param_1[0x32] = fVar20;
  param_1[0x33] = (float)(uVar5 >> 0x18) * fVar13;
  param_1[0x33] = (float)*(byte *)(param_2 + 0x24) * fVar13;
  param_1[0x38] = (float)*(byte *)(param_2 + 0xd8);
  param_1[0x39] = (float)*(int *)(param_2 + 0xdc);
  fVar15 = fVar19;
  if (*(longlong *)(param_2 + 0xe0) == 0) {
    fVar15 = 0.0;
  }
  param_1[0x3a] = fVar15;
  if (*(char *)(param_2 + 0xf0) != '\0') {
    uStack_a8 = *(undefined8 *)(param_2 + 0xf4);
    uStack_a0 = *(undefined8 *)(param_2 + 0xfc);
    *(undefined8 *)(param_1 + 0x30) = uStack_a8;
    *(ulonglong *)(param_1 + 0x32) = uStack_a0;
    puVar11 = (undefined8 *)FUN_1421dfd90(&uStack_a8,*(undefined4 *)(param_2 + 0x104));
    uVar9 = puVar11[1];
    *(undefined8 *)(param_1 + 0x34) = *puVar11;
    *(undefined8 *)(param_1 + 0x36) = uVar9;
    if (*(char *)(param_2 + 0xf0) == '\0') {
      fVar19 = 0.0;
    }
    param_1[0x37] = fVar19;
  }
  fVar15 = 0.0;
  fVar17 = fVar15;
  if ((*(longlong *)(param_2 + 0x90) != 0) &&
     (fVar17 = 0.0, (*(byte *)(*(longlong *)(param_2 + 0x90) + 0x2c) & 8) != 0)) {
    fVar17 = 2.8026e-45;
  }
  lVar1 = *(longlong *)(param_2 + 0xb0);
  fVar13 = (float)((uint)fVar17 | 4);
  if (*(longlong *)(param_2 + 0x58) == 0) {
    fVar13 = fVar17;
  }
  fVar17 = (float)((uint)fVar13 | 8);
  if (*(longlong *)(param_2 + 0xa0) == 0) {
    fVar17 = fVar13;
  }
  fVar13 = (float)((uint)fVar17 | 0x10);
  if (*(longlong *)(param_2 + 0x48) == 0) {
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
  if (*(char *)(param_2 + 0x10a) == '\0') {
    fVar13 = fVar17;
  }
  if (*(float *)(param_2 + 0x6c) != 0.0) {
    fVar13 = (float)((uint)fVar13 | 0x400);
  }
  lVar8 = *(longlong *)(param_2 + 0x70);
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
  if ((*(char *)(param_2 + 0x10c) != -1) &&
     (fVar17 = (float)((uint)fVar13 | 0x20000), *(char *)(param_2 + 0x10c) == '\0')) {
    fVar17 = (float)((uint)fVar13 | 0x60000);
  }
  param_1[0x3c] = fVar17;
  if ((*(char *)(param_2 + 0x108) != '\0') &&
     (fVar15 = 2.8026e-45, *(char *)(param_2 + 0x108) == '\x01')) {
    fVar15 = 8.40779e-45;
  }
  fVar17 = (float)((uint)fVar15 | 0x10);
  if (*(char *)(param_2 + 0x10d) == -1) {
    fVar17 = fVar15;
  }
  fVar15 = (float)((uint)fVar17 | 0x20);
  if (*(char *)(param_2 + 0x10e) == -1) {
    fVar15 = fVar17;
  }
  fVar17 = (float)((uint)fVar15 | 0x40);
  if ((*(byte *)(param_2 + 0x109) & 1) == 0) {
    fVar17 = fVar15;
  }
  fVar15 = (float)((uint)fVar17 | 0x80);
  if ((*(byte *)(param_2 + 0x109) & 2) == 0) {
    fVar15 = fVar17;
  }
  fVar17 = (float)((uint)fVar15 | 0x100);
  if (*(char *)(param_2 + 0x10f) == -1) {
    fVar17 = fVar15;
  }
  fVar15 = (float)((uint)fVar17 | 0x200);
  if (*(char *)(param_2 + 0x120) == '\0') {
    fVar15 = fVar17;
  }
  fVar17 = (float)((uint)fVar15 | 0x400);
  if (*(char *)(param_2 + 0x10b) == '\0') {
    fVar17 = fVar15;
  }
  param_1[0x3d] = fVar17;
  return;
}

