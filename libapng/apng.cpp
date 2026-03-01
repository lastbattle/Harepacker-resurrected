//originally tga2apng.cpp v.2, changed to libapng following changes by haha01haha01
//----------------------------------------------------------
//Copyright (C) 2008 MaxSt ( maxst@hiend3d.com )
//Copyright (C) 2015 haha01haha01
//
//This program is free software; you can redistribute it and/or
//modify it under the terms of the GNU General Public License
//as published by the Free Software Foundation; either
//version 2 of the License, or (at your option) any later
//version.
//
//This program is distributed in the hope that it will be useful,
//but WITHOUT ANY WARRANTY; without even the implied warranty of
//MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
//GNU General Public License for more details.
//
//You should have received a copy of the GNU General Public License
//along with this program; if not, write to the Free Software
//Foundation, Inc., 675 Mass Ave, Cambridge, MA 02139, USA.

#include <stdlib.h>
#include <stdio.h>
#include <string.h>
#include <math.h>
#include <windows.h>
#include "libapng\png.h"

extern "C"
{
typedef struct { unsigned char * p; int num; int den; } _FRAME;

_FRAME Frame[100000];

unsigned char dispose(int a, int b, unsigned char * pPrev, int n, int xres, int yres, int bpp, int w0, int h0, int x0, int y0, int *w1, int *h1, int *x1, int *y1)
{
  int  i, j, k, diff, area1, area2, area3;
  int  x_min, x_max, y_min, y_max;
  unsigned char op;

  unsigned char * pImg  = Frame[a].p;
  unsigned char * pNext = Frame[b].p;

  // NONE
  x_min = xres-1;
  x_max = 0;
  y_min = yres-1;
  y_max = 0;

  for (j=0; j<yres; j++)
  for (i=0; i<xres; i++)
  {
    diff = 0;
    for (k=0; k<bpp; k++)
      if (*(pImg+(j*xres+i)*bpp+k) != *(pNext+(j*xres+i)*bpp+k)) 
        diff = 1;

    if (diff == 1)
    {
      if (i<x_min) x_min = i;
      if (i>x_max) x_max = i;
      if (j<y_min) y_min = j;
      if (j>y_max) y_max = j;
    }
  }
  
  if ((x_max<x_min) || (y_max<y_min))
  {
    *w1 = 1; *h1 = 1;
    *x1 = 0; *y1 = 0;
    return PNG_DISPOSE_OP_NONE;
  }

  area1 = (x_max-x_min+1)*(y_max-y_min+1);
  *w1 = x_max-x_min+1;
  *h1 = y_max-y_min+1;
  *x1 = x_min;
  *y1 = y_min;
  op = PNG_DISPOSE_OP_NONE;

  if (a == 0)
    return op;

  // PREVIOUS
  x_min = xres-1;
  x_max = 0;
  y_min = yres-1;
  y_max = 0;

  for (j=0; j<yres; j++)
  for (i=0; i<xres; i++)
  {
    diff = 0;

    for (k=0; k<bpp; k++)
      if (*(pPrev+(j*xres+i)*bpp+k) != *(pNext+(j*xres+i)*bpp+k))
        diff = 1;

    if (diff == 1)
    {
      if (i<x_min) x_min = i;
      if (i>x_max) x_max = i;
      if (j<y_min) y_min = j;
      if (j>y_max) y_max = j;
    }
  }

  if ((x_max<x_min) || (y_max<y_min))
  {
    *w1 = 1; *h1 = 1;
    *x1 = 0; *y1 = 0;
    return PNG_DISPOSE_OP_PREVIOUS;
  }

  area2 = (x_max-x_min+1)*(y_max-y_min+1);
  if (area2 < area1)
  {
    area1 = area2;
    *w1 = x_max-x_min+1;
    *h1 = y_max-y_min+1;
    *x1 = x_min;
    *y1 = y_min;
    op = PNG_DISPOSE_OP_PREVIOUS;
  }

  // BACKGROUND
  if (bpp == 4)
  {
    x_min = xres-1;
    x_max = 0;
    y_min = yres-1;
    y_max = 0;

    for (j=0; j<yres; j++)
    for (i=0; i<xres; i++)
    {
      diff = 0;

      if ((i>=x0) && (i<x0+w0) && (j>=y0) && (j<y0+h0))
      {
        for (k=0; k<bpp; k++)
          if (*(pNext+(j*xres+i)*bpp+k) != 0) 
            diff = 1;
      }
      else
      {
        for (k=0; k<bpp; k++)
          if (*(pImg+(j*xres+i)*bpp+k) != *(pNext+(j*xres+i)*bpp+k)) 
            diff = 1;
      }

      if (diff == 1)
      {
        if (i<x_min) x_min = i;
        if (i>x_max) x_max = i;
        if (j<y_min) y_min = j;
        if (j>y_max) y_max = j;
      }
    }

    if ((x_max<x_min) || (y_max<y_min))
    {
      *w1 = 1; *h1 = 1;
      *x1 = 0; *y1 = 0;
      return PNG_DISPOSE_OP_BACKGROUND;
    }

    area3 = (x_max-x_min+1)*(y_max-y_min+1);
    if (area3 < area1)
    {
      area1 = area3;
      *w1 = x_max-x_min+1;
      *h1 = y_max-y_min+1;
      *x1 = x_min;
      *y1 = y_min;
      op = PNG_DISPOSE_OP_BACKGROUND;
    }
  }
  return op;
}

__declspec(dllexport) void CreateFrame(unsigned char * pdata, int num, int den, int i, int len)
{
	LPDWORD resu = 0;
	Frame[i].num = num;
	Frame[i].den = den;
	Frame[i].p = (unsigned char *)malloc(len);
	memcpy(Frame[i].p, pdata, len);
}

#ifdef DEBUG
void logMessage(FILE* logFile, char* text)
{
	fwrite(text,1,strlen(text),logFile);
}
#endif


__declspec(dllexport) void SaveAPNG(char * szImage, int n, int xres, int yres, int bpp, unsigned char first)
{
  FILE   * f;
  int      a, i, j, k;
  int      x0, y0, w0, h0;
  int      x1, y1, w1, h1;
  unsigned char dispose_op = PNG_DISPOSE_OP_NONE;
  png_structp png_ptr;
  png_infop info_ptr;
#ifdef DEBUG
  FILE * logFile = fopen("apngLogger.txt", "ab");

  char* tmpString = (char*)malloc(1024);
  sprintf(tmpString, "saving %s (%d frames) ... \r\n", szImage, n);
  logMessage(logFile,tmpString);
#endif
  unsigned char * pDisp=(unsigned char *)malloc(xres*yres*bpp);
  if (pDisp == NULL)
  {
#ifdef DEBUG
    logMessage(logFile,"memory error\r\n");
#endif
    return;
  }

  if ((f = fopen(szImage, "wb")) != 0) 
  {
    png_ptr = png_create_write_struct(PNG_LIBPNG_VER_STRING, NULL, NULL, NULL);

    if (png_ptr != NULL)
    {
      info_ptr = png_create_info_struct(png_ptr);

      if (info_ptr != NULL)
      {
        if (!setjmp(png_jmpbuf(png_ptr)))
        {
          png_init_io(png_ptr, f);

          png_set_IHDR(png_ptr, info_ptr, xres, yres, 8, 
            (bpp == 4) ? PNG_COLOR_TYPE_RGB_ALPHA : (bpp == 3) ? PNG_COLOR_TYPE_RGB : PNG_COLOR_TYPE_GRAY,
            PNG_INTERLACE_NONE, PNG_COMPRESSION_TYPE_BASE, PNG_FILTER_TYPE_BASE);

          png_set_acTL(png_ptr, info_ptr, n, 0);
          png_set_first_frame_is_hidden(png_ptr, info_ptr, first);
          png_write_info(png_ptr, info_ptr);

          png_bytep * row_pointers = (png_bytepp)png_malloc(png_ptr, sizeof(png_bytep) * yres);

          w0 = xres;
          h0 = yres;
          x0 = 0;
          y0 = 0;

          for (k=0; k<xres*yres*bpp; k++)
            *(pDisp+k) = 0;

          for (a=0; a<n; a++)
          {
            png_set_bgr(png_ptr);

            if (a<n-1)
              dispose_op = dispose(a, a+1, pDisp, n, xres, yres, bpp, w0, h0, x0, y0, &w1, &h1, &x1, &y1);
            else
              dispose_op = PNG_DISPOSE_OP_NONE;

            for (k=0; k<h0; k++)
              row_pointers[k] = Frame[a].p+((k+y0)*xres+x0)*bpp;

            png_write_frame_head(png_ptr, info_ptr, row_pointers, w0, h0, x0, y0, 
                                 Frame[a].num, Frame[a].den, dispose_op, PNG_BLEND_OP_SOURCE);
            png_write_image(png_ptr, row_pointers);
            png_write_frame_tail(png_ptr, info_ptr);

            if ((first==0) || (a!=0))
            {
              if (dispose_op!=PNG_DISPOSE_OP_PREVIOUS)
              {
                memcpy(pDisp, Frame[a].p, xres*yres*bpp);
                if (dispose_op==PNG_DISPOSE_OP_BACKGROUND)
                {
                  for (j=y0; j<y0+h0; j++)
                  for (i=x0; i<x0+w0; i++)
                  for (k=0; k<bpp; k++)
                    *(pDisp+(j*xres+i)*bpp+k) = 0;
                }
              }
              w0 = w1;
              h0 = h1;
              x0 = x1;
              y0 = y1;
            }
          }

          png_write_end(png_ptr, info_ptr);
          free(row_pointers);
          png_destroy_write_struct(&png_ptr, &info_ptr);
#ifdef DEBUG
          logMessage(logFile," OK");
#endif
        }
        else
          png_destroy_write_struct(&png_ptr, &info_ptr);
      }
      else
        png_destroy_write_struct(&png_ptr, (png_infopp)NULL);
    }
    fclose(f);
  }
#ifdef DEBUG
  else
    logMessage(logFile,"Error: can't open the file\r\n");
#endif

  free(pDisp);
#ifdef DEBUG
  fclose(logFile);
#endif
}
}