;;; CAD 递增刷
;;; 命令：INC_BRUSH 或 IBRUSH
;;; 选择一个末尾带数字的基准文字后，连续点选目标文字；每次点选都会递增一次。

(defun ib:digit-p (code)
  (and code (>= code 48) (<= code 57))
)

(defun ib:split-tail-number (value / index length)
  (setq length (strlen value)
        index length)
  (while (and (> index 0)
              (ib:digit-p (ascii (substr value index 1))))
    (setq index (1- index))
  )
  (if (< index length)
    (list
      (if (> index 0) (substr value 1 index) "")
      (atoi (substr value (1+ index)))
      (- length index)
    )
  )
)

(defun ib:pad-number (number width / value)
  (setq value (itoa number))
  (while (< (strlen value) width)
    (setq value (strcat "0" value))
  )
  value
)

(defun ib:text-entity-p (entity / kind)
  (setq kind (cdr (assoc 0 (entget entity))))
  (member kind '("TEXT" "MTEXT" "ATTRIB" "ATTDEF"))
)

(defun ib:set-text-com (entity value / object)
  ;; 通过 TextString 写回，避免 entmod 重组 MTEXT/属性文字时产生 DXF 组码错误。
  (setq object (vlax-ename->vla-object entity))
  (vla-put-TextString object value)
  (vla-Update object)
  T
)

(defun ib:set-text (entity value / result)
  (vl-load-com)
  (setq result (vl-catch-all-apply 'ib:set-text-com (list entity value)))
  (if (vl-catch-all-error-p result) nil result)
)

(defun ib:run (/ base-pick base-entity base-data base-text parsed prefix current width target-pick target-entity next-value)
  (setq base-pick (entsel "\n选择基准文字（末尾必须带数字）："))
  (if base-pick
    (progn
      (setq base-entity (car base-pick)
            base-data (entget base-entity)
            base-text (cdr (assoc 1 base-data)))
      (cond
        ((not (ib:text-entity-p base-entity))
          (princ "\n基准对象必须是 TEXT、MTEXT 或属性文字。")
        )
        ((null base-text)
          (princ "\n无法读取基准文字内容。")
        )
        ((null (setq parsed (ib:split-tail-number base-text)))
          (princ "\n基准文字末尾没有可递增的数字。")
        )
        (T
          (setq prefix (car parsed)
                current (cadr parsed)
                width (if (= (caddr parsed) 2) 2 (max 3 (caddr parsed))))
          (princ
            (strcat
              "\n递增刷已启动，下一个编号："
              (strcat prefix (ib:pad-number (1+ current) width))
              "；回车结束。"
            )
          )
          (while (setq target-pick (entsel "\n选择下一个文字："))
            (setq target-entity (car target-pick))
            (if (ib:text-entity-p target-entity)
              (progn
                (setq next-value (strcat prefix (ib:pad-number (1+ current) width)))
                (if (ib:set-text target-entity next-value)
                  (progn
                    (setq current (1+ current))
                    (princ (strcat "\n已写入：" next-value))
                  )
                  (princ "\n写入失败，未递增编号。")
                )
              )
              (princ "\n请选择 TEXT、MTEXT 或属性文字。")
            )
          )
          (princ "\n递增刷结束。")
        )
      )
    )
  )
  (princ)
)

(defun c:INC_BRUSH () (ib:run))
(defun c:IBRUSH () (ib:run))

(princ "\n递增刷已加载，命令：INC_BRUSH 或 IBRUSH")
(princ)
