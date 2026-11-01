// FUN_14224ecf0 @ 14224ecf0

/* WARNING: Globals starting with '_' overlap smaller symbols at the same address */

void FUN_14224ecf0(uint *param_1,longlong param_2,float param_3,int param_4)

{
  undefined8 uVar1;
  uint uVar2;
  uint uVar3;
  float fVar4;
  uint uVar5;
  float fVar6;
  float fVar7;
  float fVar8;
  float fVar9;
  float fVar10;
  float fVar11;
  float fVar12;
  uint uStack_b8;
  uint uStack_b4;
  
  fVar9 = _UNK_143c844b4;
  uVar1 = *(undefined8 *)(param_2 + 0x30);
  fVar8 = *(float *)(param_2 + 0x20);
  fVar4 = *(float *)(param_2 + 0x24);
  fVar10 = *(float *)(param_2 + 0x28);
  uVar5 = *(uint *)(param_2 + 0x38);
  fVar7 = *(float *)(param_2 + 0x50) * *(float *)(param_2 + 0x70) * *(float *)(param_2 + 0x4c);
  if ((*(byte *)(param_2 + 0x6d) & 1) != 0) {
    fVar7 = fVar7 * param_3;
  }
  fVar11 = fVar7 * *(float *)(param_2 + 0x40);
  fVar12 = fVar7 * *(float *)(param_2 + 0x44);
  fVar7 = fVar7 * *(float *)(param_2 + 0x48);
  if ((*(char *)(param_2 + 0x6c) != '\x02') &&
     (((*(byte *)(param_2 + 0x6d) & 2) != 0 || (cRam000000014502deac != '\0')))) {
    fVar11 = fVar11 * _UNK_14355c10c;
    fVar12 = fVar12 * _UNK_14355c10c;
    fVar7 = fVar7 * _UNK_14355c10c;
  }
  fVar6 = fVar4 * fVar4 + fVar8 * fVar8 + fVar10 * fVar10;
  if (_UNK_143c84348 <= fVar6) {
    fVar6 = _UNK_143c844b4 / SQRT(fVar6);
    fVar8 = fVar8 * fVar6;
    fVar4 = fVar4 * fVar6;
    fVar10 = fVar10 * fVar6;
  }
  else {
    fVar8 = 0.0;
    fVar10 = 0.0;
    fVar4 = _UNK_143c844b4;
  }
  uVar3 = FUN_14224f010(param_2);
  uVar2 = _UNK_143c847f0;
  param_1[3] = uVar3;
  if (*(char *)(param_2 + 0x6c) == '\x02') {
    *param_1 = (uint)fVar8 ^ uVar2;
    param_1[1] = (uint)fVar4 ^ uVar2;
    fVar6 = 0.0;
    param_1[2] = (uint)fVar10 ^ uVar2;
  }
  else {
    uStack_b8 = (uint)uVar1;
    uStack_b4 = (uint)((ulonglong)uVar1 >> 0x20);
    *param_1 = uStack_b8;
    param_1[2] = uVar5;
    param_1[1] = uStack_b4;
    fVar6 = *(float *)(param_2 + 0x5c) * *(float *)(param_2 + 0x58);
    fVar6 = fVar6 * fVar6;
    if (((*(byte *)(param_2 + 0x6d) & 4) == 0) ||
       ((*(char *)(param_2 + 0x6c) != '\x02' &&
        (((*(byte *)(param_2 + 0x6d) & 2) != 0 || (cRam000000014502deac != '\0')))))) {
      fVar6 = fVar9 / (fVar6 * fVar6);
    }
    else {
      fVar6 = (float)((uint)(fVar6 * fVar6) ^ uVar2);
    }
  }
  param_1[7] = (uint)fVar6;
  fVar6 = 0.0;
  param_1[4] = (uint)fVar11;
  param_1[5] = (uint)fVar12;
  param_1[6] = (uint)fVar7;
  param_1[8] = (uint)fVar8;
  param_1[9] = (uint)fVar4;
  param_1[10] = (uint)fVar10;
  if (*(char *)(param_2 + 0x6c) == '\x01') {
    fVar8 = (float)func_0x000143324bd9(*(float *)(param_2 + 0x60) * _UNK_143560154);
    fVar4 = (float)func_0x000143324bd9();
    fVar6 = fVar4 - fVar8;
    if (fVar4 - fVar8 <= _UNK_143c8435c) {
      fVar6 = _UNK_143c8435c;
    }
    fVar6 = fVar9 / fVar6;
    fVar9 = (float)((uint)(fVar8 * fVar6) ^ uVar2);
  }
  param_1[0xb] = (uint)fVar9;
  param_1[0xc] = (uint)fVar6;
  param_1[0xd] = (uint)(float)param_4;
  uVar5 = *(uint *)(param_2 + 0x68);
  if ((*(byte *)(param_2 + 0x6d) & 8) == 0) {
    uVar5 = uVar5 ^ uVar2;
  }
  param_1[0xe] = uVar5;
  if ((*(byte *)(param_2 + 0x6d) & 0x10) == 0) {
    param_1[0xf] = (uint)(*(float *)(param_2 + 0x74) / *(float *)(param_2 + 0x70));
  }
  else {
    param_1[0xf] = 0;
  }
  return;
}

